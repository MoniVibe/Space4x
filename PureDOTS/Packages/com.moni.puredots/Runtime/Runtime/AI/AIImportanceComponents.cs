using Unity.Entities;

namespace PureDOTS.Runtime.AI
{
    /// <summary>
    /// AI importance/LOD level for entities.
    /// Determines update frequency and AI detail level.
    /// Level 0 = hero/cinematic (full detail), Level 3 = background noise (minimal detail).
    /// </summary>
    public struct AIImportance : IComponentData
    {
        /// <summary>
        /// Importance level (0-3).
        /// 0 = Hero/Cinematic - full behavior tree, frequent updates, detailed nav
        /// 1 = Important - standard AI, regular updates
        /// 2 = Normal - simplified AI, throttled updates
        /// 3 = Background - script-like patterns, minimal updates
        /// </summary>
        public byte Level;

        /// <summary>
        /// Creates importance component with specified level.
        /// </summary>
        public static AIImportance Create(byte level)
        {
            return new AIImportance
            {
                Level = level > 3 ? (byte)3 : level
            };
        }

        /// <summary>
        /// Creates hero-level importance (Level 0).
        /// </summary>
        public static AIImportance Hero()
        {
            return Create(0);
        }

        /// <summary>
        /// Creates important-level importance (Level 1).
        /// </summary>
        public static AIImportance Important()
        {
            return Create(1);
        }

        /// <summary>
        /// Creates normal-level importance (Level 2).
        /// </summary>
        public static AIImportance Normal()
        {
            return Create(2);
        }

        /// <summary>
        /// Creates background-level importance (Level 3).
        /// </summary>
        public static AIImportance Background()
        {
            return Create(3);
        }
    }
}

