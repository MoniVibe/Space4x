using PureDOTS.Runtime.Identity;

namespace Space4x.Identity
{
    /// <summary>
    /// Space4X-specific identity extensions and interpretations.
    /// </summary>
    public static class Space4xIdentityExtensions
    {
        /// <summary>
        /// Space4X purity interpretation: Xenophilic egalitarian ↔ Xenophobic supremacist
        /// </summary>
        public static string GetPurityDescription(float purity)
        {
            if (purity > 50f)
                return "Xenophilic Egalitarian";
            if (purity > 20f)
                return "Xenophilic";
            if (purity > -20f)
                return "Neutral";
            if (purity > -50f)
                return "Xenophobic";
            return "Xenophobic Supremacist";
        }

        /// <summary>
        /// Space4X outlook tags: Xenophobic, Egalitarian, Authoritarian (plus shared pool)
        /// (Uses base OutlookType enum, no extensions needed)
        /// </summary>

        /// <summary>
        /// Space4X might/magic interpretation: Kinetics/armor/cybernetics ↔ Psionics/bio-tech/warp sorcery
        /// </summary>
        public static string GetMightMagicDescription(float axis)
        {
            if (axis < -30f)
                return "Might-focused (Kinetics, Armor, Cybernetics, Brute-force Tech)";
            if (axis > 30f)
                return "Magic-focused (Psionics, Bio-tech, Warp Sorcery, Eldritch Systems)";
            return "Hybrid (Symbiotic Techno-Mystic)";
        }
    }
}

