using PureDOTS.Runtime.Camera;
using UnityEngine;

namespace PureDOTS.Input
{
    public sealed class ObjectGrabRmbHandler : RmbHandlerBehaviour
    {
        [Header("Targeting")]
        [SerializeField] bool requireEmptyHand = true;

        public override bool CanHandle(in RmbContext context)
        {
            if (context.PointerOverUI) return false;
            if (!context.HitDraggable) return false;
            if (requireEmptyHand && context.HandHasCargo) return false;
            return true;
        }

        public override void OnRmb(in RmbContext context, RmbPhase phase)
        {
            Raise(context, phase);
        }
    }
}
