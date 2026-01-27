using PureDOTS.Runtime.Camera;
using UnityEngine;

namespace PureDOTS.Input
{
    public sealed class StorehouseDumpRmbHandler : RmbHandlerBehaviour
    {
        [Header("Storehouse")]
        [SerializeField] bool requireCargo = true;

        public override bool CanHandle(in RmbContext context)
        {
            if (context.PointerOverUI) return false;
            if (!context.HitStorehouse) return false;
            if (requireCargo && !context.HandHasCargo) return false;
            return true;
        }

        public override void OnRmb(in RmbContext context, RmbPhase phase)
        {
            Raise(context, phase);
        }
    }
}
