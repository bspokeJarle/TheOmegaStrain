using System;

namespace CommonUtilities.CommonGlobalState.States
{
    public sealed class WeatherVisualState
    {
        public float LightningFlashIntensity { get; private set; }
        public float ImpactFlashIntensity { get; private set; }
        public float ScreenFlashIntensity => Math.Max(LightningFlashIntensity, ImpactFlashIntensity);

        public void RaiseLightningFlash(float intensity)
        {
            LightningFlashIntensity = Math.Max(LightningFlashIntensity, Math.Clamp(intensity, 0f, 1f));
        }

        public void RaiseImpactFlash(float intensity)
        {
            ImpactFlashIntensity = Math.Max(ImpactFlashIntensity, Math.Clamp(intensity, 0f, 1f));
        }

        public void DecayLightningFlash(float amount)
        {
            LightningFlashIntensity = Math.Max(0f, LightningFlashIntensity - Math.Max(0f, amount));
        }

        public void DecayImpactFlash(float amount)
        {
            ImpactFlashIntensity = Math.Max(0f, ImpactFlashIntensity - Math.Max(0f, amount));
        }

        public void ClearLightningFlash()
        {
            LightningFlashIntensity = 0f;
            ImpactFlashIntensity = 0f;
        }
    }
}
