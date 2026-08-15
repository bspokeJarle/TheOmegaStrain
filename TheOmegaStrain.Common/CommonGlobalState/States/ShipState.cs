using Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonUtilities.CommonGlobalState.States
{
    // This class holds global state information for the ship, expand as needed
    // No deep copy for this class, it's intended to be a singleton-like static holder of state
    public class ShipState
    {
        public IVector3? ShipWorldPosition { get; set; } = null;
        public IVector3? ShipCrashCenterWorldPosition { get; set; } = null;
        public IVector3? ShipObjectOffsets { get; set; } = null;
        public IVector3? ShipVelocity { get; set; } = null;
        public bool ShipHasShadow { get; set; } = false;
        public IImpactStatus? ShipImpactStatus { get; set; } = null;
        public DateTime ShipCrashDetectionDisabledUntilUtc { get; set; } = DateTime.MinValue;
        public DateTime ShipGravityDisabledUntilUtc { get; set; } = DateTime.MinValue;
        public List<BestCandidateState> BestCandidateStates { get; set; } = [];
    }
    public class BestCandidateState
    {
        public EnemyCandidateInfo? BestEnemyCandidate { get; set; } = null;
        public DateTime TimeStampUtc { get; set; }
    }
    public class EnemyCandidateInfo
    {
        public I3dObject? EnemyObject { get; set; }
        public IVector3? EnemyCenterPosition { get; set; }
        public float DistanceToShip { get; set; }
    }
}
