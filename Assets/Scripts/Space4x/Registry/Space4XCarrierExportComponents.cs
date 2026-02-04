using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Configuration for exporting carrier-held resources into colony industry.
    /// </summary>
    public struct Space4XCarrierExportConfig : IComponentData
    {
        public float TransferRatePerSecond;
        public float MaxTransferDistance;

        public static Space4XCarrierExportConfig Default => new Space4XCarrierExportConfig
        {
            TransferRatePerSecond = 400f,
            MaxTransferDistance = 0f // 0 = unlimited
        };
    }
}
