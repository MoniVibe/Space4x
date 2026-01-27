using Unity.Entities;

namespace PureDOTS.Runtime.Interaction
{
    public struct InteractionPolicy : IComponentData
    {
        public byte AllowStructuralFallback;
        public byte LogStructuralFallback;

        public static InteractionPolicy CreateDefault()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return new InteractionPolicy
            {
                AllowStructuralFallback = 1,
                LogStructuralFallback = 1
            };
#else
            return new InteractionPolicy
            {
                AllowStructuralFallback = 0,
                LogStructuralFallback = 0
            };
#endif
        }
    }
}
