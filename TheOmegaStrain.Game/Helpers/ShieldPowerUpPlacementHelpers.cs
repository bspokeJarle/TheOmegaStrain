using System;
using System.Collections.Generic;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Game.Helpers
{
    public static class ShieldPowerUpPlacementHelpers
    {
        private const float FirstSceneCarrierRatio = 0.30f;
        private const float LastSceneCarrierRatio = 0.15f;
        private const int LastCampaignSceneNumber = 8;

        public static float GetShieldCarrierRatio(int sceneNumber)
        {
            int clampedSceneNumber = Math.Clamp(sceneNumber, 1, LastCampaignSceneNumber);
            float campaignProgress = (clampedSceneNumber - 1f) / (LastCampaignSceneNumber - 1f);
            return FirstSceneCarrierRatio + ((LastSceneCarrierRatio - FirstSceneCarrierRatio) * campaignProgress);
        }

        public static int AssignToSpaceSwans(IReadOnlyList<I3dObject> aiObjects, int sceneNumber)
        {
            if (aiObjects == null)
                return 0;

            int swanCount = 0;
            for (int i = 0; i < aiObjects.Count; i++)
            {
                if (aiObjects[i].ObjectName == "SpaceSwan")
                    swanCount++;
            }

            float carrierRatio = GetShieldCarrierRatio(sceneNumber);
            int shieldsRemaining = (int)Math.Round(swanCount * carrierRatio, MidpointRounding.AwayFromZero);
            int shieldsAssigned = 0;
            for (int i = 0; i < aiObjects.Count; i++)
            {
                var candidate = aiObjects[i];
                if (candidate.ObjectName != "SpaceSwan")
                    continue;

                bool carriesShield = shieldsRemaining > 0;
                candidate.HasPowerUp = carriesShield;
                candidate.PowerUpType = carriesShield ? PowerUpType.Shield : PowerUpType.Standard;

                if (carriesShield)
                {
                    shieldsRemaining--;
                    shieldsAssigned++;
                }
            }

            return shieldsAssigned;
        }
    }
}
