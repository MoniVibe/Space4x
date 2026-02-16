using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Presentation
{
    public sealed class Space4XSpectatorRenderBridgeConfigAuthoring : MonoBehaviour
    {
        public bool Enabled = true;
        public bool OnlyCapitalBattleScenarios = true;
        [Min(0.1f)] public float CapitalScale = Space4XSpectatorRenderBridgeConfig.Default.CapitalScale;
        [Min(0.05f)] public float FighterScale = Space4XSpectatorRenderBridgeConfig.Default.FighterScale;
        public Color Side0Color = new Color(0.26f, 0.62f, 1f, 1f);
        public Color Side1Color = new Color(1f, 0.4f, 0.2f, 1f);
    }

    public sealed class Space4XSpectatorRenderBridgeConfigBaker : Baker<Space4XSpectatorRenderBridgeConfigAuthoring>
    {
        public override void Bake(Space4XSpectatorRenderBridgeConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new Space4XSpectatorRenderBridgeConfig
            {
                Enabled = (byte)(authoring.Enabled ? 1 : 0),
                OnlyCapitalBattleScenarios = (byte)(authoring.OnlyCapitalBattleScenarios ? 1 : 0),
                CapitalScale = math.max(0.1f, authoring.CapitalScale),
                FighterScale = math.max(0.05f, authoring.FighterScale),
                Side0Color = new float4(authoring.Side0Color.r, authoring.Side0Color.g, authoring.Side0Color.b, authoring.Side0Color.a),
                Side1Color = new float4(authoring.Side1Color.r, authoring.Side1Color.g, authoring.Side1Color.b, authoring.Side1Color.a)
            });
        }
    }
}
