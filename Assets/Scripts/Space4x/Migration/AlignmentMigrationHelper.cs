using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Migration
{
    /// <summary>
    /// Helper functions to migrate Space4X's Law/Good/Integrity alignment to unified Moral/Order/Purity.
    /// This is a temporary migration utility. Once migration is complete, Space4X systems should use
    /// PureDOTS.Runtime.Individual.AlignmentTriplet directly.
    /// </summary>
    public static class AlignmentMigrationHelper
    {
        /// <summary>
        /// Convert Space4X AlignmentTriplet (Law/Good/Integrity) to unified AlignmentTriplet (Moral/Order/Purity).
        /// Mapping:
        /// - Law → Order (direct mapping)
        /// - Good → Moral (direct mapping)
        /// - Integrity → Purity (direct mapping)
        /// </summary>
        public static PureDOTS.Runtime.Individual.AlignmentTriplet ToUnified(AlignmentTriplet space4x)
        {
            return PureDOTS.Runtime.Individual.AlignmentTriplet.FromFloats(
                (float)space4x.Good,      // Good → Moral
                (float)space4x.Law,        // Law → Order
                (float)space4x.Integrity    // Integrity → Purity
            );
        }

        /// <summary>
        /// Convert unified AlignmentTriplet (Moral/Order/Purity) to Space4X AlignmentTriplet (Law/Good/Integrity).
        /// Reverse mapping for backward compatibility during migration.
        /// </summary>
        public static AlignmentTriplet FromUnified(PureDOTS.Runtime.Individual.AlignmentTriplet unified)
        {
            return AlignmentTriplet.FromFloats(
                unified.Order,   // Order → Law
                unified.Moral,   // Moral → Good
                unified.Purity   // Purity → Integrity
            );
        }
    }
}

