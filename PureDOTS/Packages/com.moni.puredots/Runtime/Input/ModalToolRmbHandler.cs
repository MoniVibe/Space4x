using PureDOTS.Runtime.Camera;
using UnityEngine;

namespace PureDOTS.Input
{
    public sealed class ModalToolRmbHandler : RmbHandlerBehaviour
    {
        [Header("State")]
        [SerializeField] bool modalActive;

        public bool ModalActive
        {
            get => modalActive;
            set => modalActive = value;
        }

        public override bool CanHandle(in RmbContext context)
        {
            if (!modalActive) return false;
            if (context.PointerOverUI) return false;
            return true;
        }

        public override void OnRmb(in RmbContext context, RmbPhase phase)
        {
            Raise(context, phase);
        }
    }
}
