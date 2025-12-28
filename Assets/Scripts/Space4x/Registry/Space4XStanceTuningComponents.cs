using Unity.Entities;

namespace Space4X.Registry
{
    public struct Space4XStanceTuningConfig : IComponentData
    {
        public StanceTuningEntry Aggressive;
        public StanceTuningEntry Balanced;
        public StanceTuningEntry Defensive;
        public StanceTuningEntry Evasive;

        public static Space4XStanceTuningConfig Default => new Space4XStanceTuningConfig
        {
            Aggressive = new StanceTuningEntry
            {
                AvoidanceRadius = 10f,
                AvoidanceStrength = 0.2f,
                SpeedMultiplier = 1.1f,
                RotationMultiplier = 1.2f,
                MaintainFormationWhenAttacking = 0f,
                EvasionJinkStrength = 0.05f,
                AutoEngageRadius = 600f,
                AbortAttackOnDamageThreshold = 0.2f,
                ReturnToPatrolAfterCombat = 1,
                CommandOverrideDropsToNeutral = 1,
                AttackMoveBearingWeight = 0.7f,
                AttackMoveDestinationWeight = 0.3f
            },
            Balanced = new StanceTuningEntry
            {
                AvoidanceRadius = 30f,
                AvoidanceStrength = 0.5f,
                SpeedMultiplier = 1.0f,
                RotationMultiplier = 1.0f,
                MaintainFormationWhenAttacking = 1f,
                EvasionJinkStrength = 0.1f,
                AutoEngageRadius = 400f,
                AbortAttackOnDamageThreshold = 0.3f,
                ReturnToPatrolAfterCombat = 1,
                CommandOverrideDropsToNeutral = 0,
                AttackMoveBearingWeight = 0.5f,
                AttackMoveDestinationWeight = 0.5f
            },
            Defensive = new StanceTuningEntry
            {
                AvoidanceRadius = 50f,
                AvoidanceStrength = 0.8f,
                SpeedMultiplier = 0.95f,
                RotationMultiplier = 1.1f,
                MaintainFormationWhenAttacking = 1f,
                EvasionJinkStrength = 0.2f,
                AutoEngageRadius = 250f,
                AbortAttackOnDamageThreshold = 0.4f,
                ReturnToPatrolAfterCombat = 0,
                CommandOverrideDropsToNeutral = 0,
                AttackMoveBearingWeight = 0.35f,
                AttackMoveDestinationWeight = 0.65f
            },
            Evasive = new StanceTuningEntry
            {
                AvoidanceRadius = 80f,
                AvoidanceStrength = 1.2f,
                SpeedMultiplier = 0.85f,
                RotationMultiplier = 1.5f,
                MaintainFormationWhenAttacking = 0f,
                EvasionJinkStrength = 0.35f,
                AutoEngageRadius = 150f,
                AbortAttackOnDamageThreshold = 0.5f,
                ReturnToPatrolAfterCombat = 0,
                CommandOverrideDropsToNeutral = 0,
                AttackMoveBearingWeight = 0.2f,
                AttackMoveDestinationWeight = 0.8f
            }
        };

        public StanceTuningEntry Resolve(VesselStanceMode stance)
        {
            return stance switch
            {
                VesselStanceMode.Aggressive => Aggressive,
                VesselStanceMode.Defensive => Defensive,
                VesselStanceMode.Evasive => Evasive,
                _ => Balanced
            };
        }
    }

    public struct StanceTuningEntry
    {
        public float AvoidanceRadius;
        public float AvoidanceStrength;
        public float SpeedMultiplier;
        public float RotationMultiplier;
        public float MaintainFormationWhenAttacking;
        public float EvasionJinkStrength;
        public float AutoEngageRadius;
        public float AbortAttackOnDamageThreshold;
        public byte ReturnToPatrolAfterCombat;
        public byte CommandOverrideDropsToNeutral;
        public float AttackMoveBearingWeight;
        public float AttackMoveDestinationWeight;
    }
}
