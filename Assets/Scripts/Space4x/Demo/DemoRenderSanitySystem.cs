#if SPACE4X_DEMO
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Space4X.Registry;

namespace Space4X.Demo
{
    /// <summary>
    /// Emits a single PlayEffectRequest (FX.Demo.Ping) at startup so presentation bridges prove they are wired.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XDemoBootstrapSystem))]
    public partial struct DemoRenderSanitySystem : ISystem
    {
        private static bool s_once;

        [BurstDiscard]
        private static FixedString64Bytes CreatePingFxId()
        {
            var id = default(FixedString64Bytes);
            const string text = "FX.Demo.Ping";
            for (int i = 0; i < text.Length; i++)
            {
                id.Append(text[i]);
            }

            return id;
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XEffectRequestStream>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (s_once)
            {
                state.Enabled = false;
                return;
            }

            s_once = true;

            var effectStreamEntity = SystemAPI.GetSingletonEntity<Space4XEffectRequestStream>();
            if (!state.EntityManager.HasBuffer<PlayEffectRequest>(effectStreamEntity))
            {
                state.EntityManager.AddBuffer<PlayEffectRequest>(effectStreamEntity);
            }

            var buffer = state.EntityManager.GetBuffer<PlayEffectRequest>(effectStreamEntity);
            buffer.Add(new PlayEffectRequest
            {
                EffectId = CreatePingFxId(),
                AttachTo = Entity.Null,
                Lifetime = 2f
            });

            state.Enabled = false;
        }
    }
}
#endif

