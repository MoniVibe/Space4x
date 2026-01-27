using PureDOTS.Runtime.Motivation;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring.Motivation
{
    /// <summary>
    /// Authoring component for entities with motivation drives.
    /// Exposes initial initiative, loyalty, moral profile, and primary loyalty target.
    /// </summary>
    public class MotivationAuthoring : MonoBehaviour
    {
        [Header("Initiative")]
        [Tooltip("Initial initiative (0-200, 100 = baseline).")]
        [Range(0, 200)]
        public byte InitialInitiative = 100;

        [Tooltip("Maximum initiative (0-200).")]
        [Range(0, 200)]
        public byte MaxInitiative = 100;

        [Header("Loyalty")]
        [Tooltip("Initial loyalty (0-200, 0 = no loyalty, 200 = martyr).")]
        [Range(0, 200)]
        public byte InitialLoyalty = 50;

        [Tooltip("Maximum loyalty (0-200).")]
        [Range(0, 200)]
        public byte MaxLoyalty = 100;

        [Tooltip("Primary loyalty target (village, guild, fleet, empire, etc.). Leave null for none.")]
        public GameObject PrimaryLoyaltyTarget;

        [Header("Moral Profile")]
        [Tooltip("Corrupt (-100) to Pure (+100).")]
        [Range(-100, 100)]
        public sbyte CorruptPure = 0;

        [Tooltip("Chaotic (-100) to Lawful (+100).")]
        [Range(-100, 100)]
        public sbyte ChaoticLawful = 0;

        [Tooltip("Evil (-100) to Good (+100).")]
        [Range(-100, 100)]
        public sbyte EvilGood = 0;

        [Tooltip("Might (-100) to Magic (+100).")]
        [Range(-100, 100)]
        public sbyte MightMagic = 0;

        [Tooltip("Vengeful (-100) to Forgiving (+100).")]
        [Range(-100, 100)]
        public sbyte VengefulForgiving = 0;

        [Tooltip("Craven (-100) to Bold (+100).")]
        [Range(-100, 100)]
        public sbyte CravenBold = 0;

        /// <summary>
        /// Baker for MotivationAuthoring component.
        /// Creates MotivationDrive, MoralProfile, and initializes motivation infrastructure.
        /// </summary>
        public class MotivationBaker : Baker<MotivationAuthoring>
        {
            public override void Bake(MotivationAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Resolve primary loyalty target entity
                var loyaltyTargetEntity = Entity.Null;
                if (authoring.PrimaryLoyaltyTarget != null)
                {
                    loyaltyTargetEntity = GetEntity(authoring.PrimaryLoyaltyTarget, TransformUsageFlags.Dynamic);
                }

                // Add motivation drive
                AddComponent(entity, new MotivationDrive
                {
                    InitiativeCurrent = authoring.InitialInitiative,
                    InitiativeMax = authoring.MaxInitiative,
                    LoyaltyCurrent = authoring.InitialLoyalty,
                    LoyaltyMax = authoring.MaxLoyalty,
                    PrimaryLoyaltyTarget = loyaltyTargetEntity,
                    LastInitiativeTick = 0
                });

                // Add moral profile
                AddComponent(entity, new MoralProfile
                {
                    CorruptPure = (sbyte)Mathf.Clamp(authoring.CorruptPure, -100, 100),
                    ChaoticLawful = (sbyte)Mathf.Clamp(authoring.ChaoticLawful, -100, 100),
                    EvilGood = (sbyte)Mathf.Clamp(authoring.EvilGood, -100, 100),
                    MightMagic = (sbyte)Mathf.Clamp(authoring.MightMagic, -100, 100),
                    VengefulForgiving = (sbyte)Mathf.Clamp(authoring.VengefulForgiving, -100, 100),
                    CravenBold = (sbyte)Mathf.Clamp(authoring.CravenBold, -100, 100)
                });

                // MotivationInitializeSystem will add MotivationSlot buffer, MotivationIntent, and LegacyPoints
                // if they don't exist, so we don't need to add them here
            }
        }
    }
}
























