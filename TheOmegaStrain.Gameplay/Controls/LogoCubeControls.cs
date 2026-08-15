using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using System;
using System.IO;

namespace TheOmegaStrain.Gameplay.Controls
{
    public class OmegaStrainLogoControls : IObjectMovement
    {
        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        private bool exploded = false;
        private DateTime ExplosionDeltaTime;
        private bool videoTriggered = false;

        private bool introStarted = false;
        private DateTime introStartTime;

        private readonly OmegaMeshRotation _rotate = new(); // Rotation fra OmegaEngineAdapters (not used here)

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ParentObject = theObject;
            if (theObject.ImpactStatus?.HasExploded == true || GameState.ScreenOverlayState.Type == ScreenOverlayType.Game ) return theObject;

            if (!introStarted)
            {
                introStarted = true;
                introStartTime = DateTime.Now;
                exploded = false;

                // Start pose: Omega faces camera at X=90 (known anchor)
                if (theObject.Rotation != null)
                {
                    theObject.Rotation.x = 90f;
                    theObject.Rotation.y = 0f;
                    theObject.Rotation.z = 0f;
                }

                // Start outside right
                theObject.ObjectOffsets.x = ScreenSetup.screenSizeX * 0.4f;
                theObject.ObjectOffsets.z = 0f;
            }

            float t = (float)(DateTime.Now - introStartTime).TotalSeconds;
            if (t < 0f) t = 0f;

            // -------------------------
            // CONFIG (tweak these live)
            // -------------------------
            float EnterStartX = ScreenSetup.screenSizeX * 0.4f;
            const float EnterEndX = 0f;

            const float ZoomStartZ = 0f;
            const float ZoomEndZ = 900f;

            // Anchor-based angles (confirmed by you)
            const float X_Omega = 90f;
            const float X_Retro = 270f;

            // Rotate on Z
            const float Z_Start = 0f;
            const float Z_Music = -90f;
            const float Z_047 = -270f;

            // Zoom spin (added on top)
            const float ZoomSpinY = 180f;
            const float ZoomSpinZ = 360f;

            // --- NEW: pause + timing knobs ---
            const float Pause047Seconds = 1.5f;      // <--- juster denne for � holde 047 litt
            const float ZoomSeconds = 4.0f;           // zoom-lengde (var 14-18)
            const float ExplodeAtSeconds = 3.0f;     // n�r i zoom (0..ZoomSeconds) eksplosjonen skal trigges

            // Optional: if you want explode by Z instead, keep this:
            const float ExplodeAtZ = 800f;

            // -------------------------
            // Helpers
            // -------------------------
            float Lerp(float a, float b, float u) => a + (b - a) * u;
            float Smooth(float u) => u * u * (3f - 2f * u);

            float Segment01(float time, float start, float end)
            {
                if (time <= start) return 0f;
                if (time >= end) return 1f;
                return (time - start) / (end - start);
            }

            // -------------------------
            // Timeline (with pause)
            // -------------------------
            // Original:
            // 0-3 enter
            // 3-6 to retro
            // 6-9 to music
            // 9-11 hold music
            // 11-14 to 047
            // 14-18 zoom

            // New:
            // 0-3 enter
            // 3-6 to retro
            // 6-9 to music
            // 9-11 hold music
            // 11-14 to 047
            // 14-(14+Pause) hold 047   <--- NEW
            // (14+Pause)-(14+Pause+ZoomSeconds) zoom

            const float T0_EnterStart = 0f;
            const float T1_EnterEnd = 3f;

            const float T2_ToRetroEnd = 6f;
            const float T3_ToMusicEnd = 9f;

            const float T4_HoldMusicEnd = 11f;
            const float T5_To047End = 14f;

            float T6_Hold047End = T5_To047End + Pause047Seconds;
            float T7_ZoomEnd = T6_Hold047End + ZoomSeconds;

            // Clamp total length to T7 if you want hard stop
            if (t > T7_ZoomEnd) t = T7_ZoomEnd;

            float uEnter = Smooth(Segment01(t, T0_EnterStart, T1_EnterEnd));
            float uToRetro = Smooth(Segment01(t, T1_EnterEnd, T2_ToRetroEnd));
            float uToMusic = Smooth(Segment01(t, T2_ToRetroEnd, T3_ToMusicEnd));
            float uTo047 = Smooth(Segment01(t, T4_HoldMusicEnd, T5_To047End));
            float uZoom = Smooth(Segment01(t, T6_Hold047End, T7_ZoomEnd));

            // -------------------------
            // Position
            // -------------------------
            theObject.ObjectOffsets.x = Lerp(EnterStartX, EnterEndX, uEnter);
            theObject.ObjectOffsets.z = Lerp(ZoomStartZ, ZoomEndZ, uZoom);

            // -------------------------
            // Rotation
            // -------------------------
            float xRot = X_Omega;
            float yRot = 0f;
            float zRot = Z_Start;

            if (t < T1_EnterEnd)
            {
                // 0-3: Omega visible, enter only
                xRot = X_Omega;
                yRot = 0f;
                zRot = Z_Start;
            }
            else if (t < T2_ToRetroEnd)
            {
                // 3-6: rotate X to Retro
                xRot = Lerp(X_Omega, X_Retro, uToRetro);
                yRot = 0f;
                zRot = Z_Start;
            }
            else if (t < T3_ToMusicEnd)
            {
                // 6-9: rotate Z to MusicBy (keep X at Retro)
                xRot = X_Retro;
                yRot = 0f;
                zRot = Lerp(Z_Start, Z_Music, uToMusic);
            }
            else if (t < T4_HoldMusicEnd)
            {
                // 9-11: hold MusicBy
                xRot = X_Retro;
                yRot = 0f;
                zRot = Z_Music;
            }
            else if (t < T5_To047End)
            {
                // 11-14: rotate Z to 047
                xRot = X_Retro;
                yRot = 0f;
                zRot = Lerp(Z_Music, Z_047, uTo047);
            }
            else if (t < T6_Hold047End)
            {
                // 14-(14+pause): HOLD 047  <--- NEW
                xRot = X_Retro;
                yRot = 0f;
                zRot = Z_047;
            }
            else
            {
                // Zoom: keep baseline 047 + cinematic spin on top
                xRot = X_Retro;
                yRot = 0f;
                zRot = Z_047;

                yRot += Lerp(0f, ZoomSpinY, uZoom);
                zRot += Lerp(0f, ZoomSpinZ, uZoom);
            }

            if (theObject.Rotation != null && !exploded)
            {
                theObject.Rotation.x = xRot;
                theObject.Rotation.y = yRot;
                theObject.Rotation.z = zRot;
            }

            // -------------------------
            // Explode: time-based (recommended for music sync)
            // -------------------------
            float zoomStartTime = T6_Hold047End;
            float explodeTime = zoomStartTime + ExplodeAtSeconds;

            // Option A (recommended): explode at specific time
            if (t >= explodeTime)
            {
                if (!exploded)
                {
                    ExplosionDeltaTime = DateTime.Now;
                    Physics.ExplodeObject(theObject, 800f);
                    exploded = true;
                }
                else
                {
                    Physics.UpdateExplosion(theObject, ExplosionDeltaTime);

                    if (theObject.ImpactStatus?.HasExploded==true)
                    {
                        //Show overlay
                        GameState.ScreenOverlayState.ShowOverlay = true;
                        if (!videoTriggered)
                        {
                            GameState.ScreenOverlayState.ShowVideoOverlay = true;
                            GameState.ScreenOverlayState.VideoClipPath = Path.Combine("gamegraphics", "introclip.mp4");
                            videoTriggered = true;
                        }
                        //Eliminate the object
                        theObject.ObjectParts.Clear();
                    }
                }
            }
            return theObject;
        }

        public void ReleaseParticles() { }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
            throw new NotImplementedException();
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            throw new NotImplementedException();
        }

        public void ReleaseParticles(I3dObject theObject)
        {
            throw new NotImplementedException();
        }
    }
}
