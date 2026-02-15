using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Presentation
{
    public struct RenderProxyTag : IComponentData
    {
    }

    public struct RenderProxySource : IComponentData
    {
        public Entity Value;
    }

    public enum SpectatorRenderProxyKind : byte
    {
        Fighter = 0,
        Capital = 1
    }

    public struct RenderProxyKind : IComponentData
    {
        public SpectatorRenderProxyKind Value;
    }

    public struct RenderProxySide : IComponentData
    {
        public byte Value;
    }

    public struct RenderProxyLink : IComponentData
    {
        public Entity ProxyEntity;
    }

    public struct Space4XSpectatorRenderBridgeConfig : IComponentData
    {
        public byte Enabled;
        public byte OnlyCapitalBattleScenarios;
        public float CapitalScale;
        public float FighterScale;
        public float4 Side0Color;
        public float4 Side1Color;

        public static Space4XSpectatorRenderBridgeConfig Default => new Space4XSpectatorRenderBridgeConfig
        {
            Enabled = 1,
            OnlyCapitalBattleScenarios = 1,
            CapitalScale = 2.2f,
            FighterScale = 0.6f,
            Side0Color = new float4(0.26f, 0.62f, 1f, 1f),
            Side1Color = new float4(1f, 0.4f, 0.2f, 1f)
        };
    }
}
