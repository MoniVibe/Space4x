using Space4X.Registry;
using Space4X.Runtime;
using PureDOTS.Runtime.Communication;
using PureDOTS.Runtime.Modules;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component that marks an entity as a capital ship.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Capital Ship")]
    public sealed class CapitalShipAuthoring : MonoBehaviour
    {
        public sealed class Baker : Unity.Entities.Baker<CapitalShipAuthoring>
        {
            public override void Bake(CapitalShipAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<Registry.CapitalShipTag>(entity);

                AddComponent(entity, CommDecisionConfig.Default);
                AddComponent(entity, CommDecodeFactors.Default);

                AddComponent(entity, new ModulePowerSupply
                {
                    AvailablePower = 20f
                });
                AddComponent(entity, new ModuleCapabilityOutput
                {
                    ThrustAuthority = 0f,
                    TurnAuthority = 0f
                });
                AddComponent(entity, new EngineeringCohesion { Value = 0.5f });
                AddComponent(entity, new NavigationCohesion { Value = 0.5f });
                AddComponent(entity, new BridgeTechLevel { Value = 0.5f });
                AddComponent(entity, new ManeuverMode { Mode = ShipManeuverMode.Transit });
                AddComponent(entity, new OfficerProfile
                {
                    ExpectedManeuverHorizonSeconds = 6f,
                    RiskTolerance = 0.5f
                });
            }
        }
    }
}
