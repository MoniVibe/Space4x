using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Config for mapping businesses to starter hull catalog IDs.
    /// </summary>
    public struct Space4XBusinessFleetSeedConfig : IComponentData
    {
        public FixedString64Bytes DefaultHullId;
    }

    [InternalBufferCapacity(4)]
    public struct Space4XBusinessFleetSeedOverride : IBufferElementData
    {
        public Space4XBusinessKind BusinessKind;
        public FixedString64Bytes HullId;
    }

    public static class Space4XBusinessFleetSeedDefaults
    {
        public static readonly FixedString64Bytes DefaultHullId = new FixedString64Bytes("lcv-sparrow");
        public static readonly FixedString64Bytes CarrierHullId = new FixedString64Bytes("cv-mule");

        public static void ApplyDefaults(
            ref Space4XBusinessFleetSeedConfig config,
            ref DynamicBuffer<Space4XBusinessFleetSeedOverride> overrides)
        {
            config.DefaultHullId = DefaultHullId;
            overrides.Clear();

            overrides.Add(new Space4XBusinessFleetSeedOverride
            {
                BusinessKind = Space4XBusinessKind.Shipwright,
                HullId = CarrierHullId
            });
            overrides.Add(new Space4XBusinessFleetSeedOverride
            {
                BusinessKind = Space4XBusinessKind.MarketHub,
                HullId = CarrierHullId
            });
            overrides.Add(new Space4XBusinessFleetSeedOverride
            {
                BusinessKind = Space4XBusinessKind.DeepCoreSyndicate,
                HullId = CarrierHullId
            });
        }
    }
}
