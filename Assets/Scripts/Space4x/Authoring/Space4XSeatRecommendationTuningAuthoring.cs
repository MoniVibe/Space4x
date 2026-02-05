using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Seat Recommendation Tuning")]
    public sealed class Space4XSeatRecommendationTuningAuthoring : MonoBehaviour
    {
        [System.Serializable]
        public struct CombatSettings
        {
            [Range(0f, 1f)] public float minWeaponsOnlineRatio;
            [Range(0, 255)] public int minContacts;
            [Range(0, 255)] public int attackPriorityBase;
            [Range(0, 255)] public int attackPriorityPerContact;
            [Range(0, 255)] public int attackPriorityMin;

            public CombatSettings Clamp()
            {
                minWeaponsOnlineRatio = math.clamp(minWeaponsOnlineRatio, 0f, 1f);
                minContacts = math.clamp(minContacts, 0, 255);
                attackPriorityBase = math.clamp(attackPriorityBase, 0, 255);
                attackPriorityPerContact = math.clamp(attackPriorityPerContact, 0, 255);
                attackPriorityMin = math.clamp(attackPriorityMin, 0, 255);
                return this;
            }
        }

        [System.Serializable]
        public struct SensorsSettings
        {
            [Range(0f, 10000f)] public float minSensorRange;
            [Range(0, 255)] public int patrolPriority;
            [Range(0, 255)] public int interceptPriority;

            public SensorsSettings Clamp()
            {
                minSensorRange = math.max(0f, minSensorRange);
                patrolPriority = math.clamp(patrolPriority, 0, 255);
                interceptPriority = math.clamp(interceptPriority, 0, 255);
                return this;
            }
        }

        [System.Serializable]
        public struct LogisticsSettings
        {
            [Range(0f, 1f)] public float retreatHullRatio;
            [Range(0f, 1f)] public float resupplyFuelRatio;
            [Range(0f, 1f)] public float resupplyAmmoRatio;
            [Range(0, 255)] public int retreatPriority;
            [Range(0, 255)] public int resupplyPriority;

            public LogisticsSettings Clamp()
            {
                retreatHullRatio = math.clamp(retreatHullRatio, 0f, 1f);
                resupplyFuelRatio = math.clamp(resupplyFuelRatio, 0f, 1f);
                resupplyAmmoRatio = math.clamp(resupplyAmmoRatio, 0f, 1f);
                retreatPriority = math.clamp(retreatPriority, 0, 255);
                resupplyPriority = math.clamp(resupplyPriority, 0, 255);
                return this;
            }
        }

        [Header("Combat")]
        public CombatSettings combat = new CombatSettings
        {
            minWeaponsOnlineRatio = 0.1f,
            minContacts = 1,
            attackPriorityBase = 80,
            attackPriorityPerContact = 10,
            attackPriorityMin = 10
        };

        [Header("Sensors")]
        public SensorsSettings sensors = new SensorsSettings
        {
            minSensorRange = 0f,
            patrolPriority = 80,
            interceptPriority = 60
        };

        [Header("Logistics")]
        public LogisticsSettings logistics = new LogisticsSettings
        {
            retreatHullRatio = 0.3f,
            resupplyFuelRatio = 0.25f,
            resupplyAmmoRatio = 0.2f,
            retreatPriority = 20,
            resupplyPriority = 40
        };

        private void OnValidate()
        {
            combat = combat.Clamp();
            sensors = sensors.Clamp();
            logistics = logistics.Clamp();
        }

        private sealed class Baker : Baker<Space4XSeatRecommendationTuningAuthoring>
        {
            public override void Bake(Space4XSeatRecommendationTuningAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                var combat = authoring.combat.Clamp();
                var sensors = authoring.sensors.Clamp();
                var logistics = authoring.logistics.Clamp();

                AddComponent(entity, new SeatRecommendationTuning
                {
                    Combat = new CombatRecommendationTuning
                    {
                        MinWeaponsOnlineRatio = math.clamp(combat.minWeaponsOnlineRatio, 0f, 1f),
                        MinContacts = (byte)math.clamp(combat.minContacts, 0, 255),
                        AttackPriorityBase = (byte)math.clamp(combat.attackPriorityBase, 0, 255),
                        AttackPriorityPerContact = (byte)math.clamp(combat.attackPriorityPerContact, 0, 255),
                        AttackPriorityMin = (byte)math.clamp(combat.attackPriorityMin, 0, 255)
                    },
                    Sensors = new SensorsRecommendationTuning
                    {
                        MinSensorRange = math.max(0f, sensors.minSensorRange),
                        PatrolPriority = (byte)math.clamp(sensors.patrolPriority, 0, 255),
                        InterceptPriority = (byte)math.clamp(sensors.interceptPriority, 0, 255)
                    },
                    Logistics = new LogisticsRecommendationTuning
                    {
                        RetreatHullRatio = math.clamp(logistics.retreatHullRatio, 0f, 1f),
                        ResupplyFuelRatio = math.clamp(logistics.resupplyFuelRatio, 0f, 1f),
                        ResupplyAmmoRatio = math.clamp(logistics.resupplyAmmoRatio, 0f, 1f),
                        RetreatPriority = (byte)math.clamp(logistics.retreatPriority, 0, 255),
                        ResupplyPriority = (byte)math.clamp(logistics.resupplyPriority, 0, 255)
                    }
                });
            }
        }
    }
}
