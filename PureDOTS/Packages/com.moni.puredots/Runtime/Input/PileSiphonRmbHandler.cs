using PureDOTS.Runtime.Camera;
using UnityEngine;

namespace PureDOTS.Input
{
    public sealed class PileSiphonRmbHandler : RmbHandlerBehaviour
    {
        [Header("Pile Siphon")]
        [SerializeField] bool requireEmptyHand;

        public override bool CanHandle(in RmbContext context)
        {
            if (context.PointerOverUI) return false;
            if (!context.HitPile) return false;
            if (requireEmptyHand && context.HandHasCargo) return false;
            return true;
        }

        public override void OnRmb(in RmbContext context, RmbPhase phase)
        {
            Raise(context, phase);
        }
    }
}
