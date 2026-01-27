#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using UnityEngine;

namespace PureDOTS.Runtime.Space
{
    /// <summary>
    /// Properties for a stellar class.
    /// Defines typical mass, luminosity, and temperature ranges.
    /// </summary>
    [Serializable]
    public struct StellarClassProperties
    {
        [Tooltip("Typical mass in solar masses.")]
        public float TypicalMass;

        [Tooltip("Typical luminosity relative to Sun (1.0 = Sun's luminosity).")]
        public float TypicalLuminosity;

        [Tooltip("Typical surface temperature in Kelvin.")]
        public float TypicalTemperature;

        [Tooltip("Minimum mass in solar masses.")]
        public float MinMass;

        [Tooltip("Maximum mass in solar masses.")]
        public float MaxMass;

        [Tooltip("Minimum luminosity relative to Sun.")]
        public float MinLuminosity;

        [Tooltip("Maximum luminosity relative to Sun.")]
        public float MaxLuminosity;

        [Tooltip("Minimum temperature in Kelvin.")]
        public float MinTemperature;

        [Tooltip("Maximum temperature in Kelvin.")]
        public float MaxTemperature;
    }

    /// <summary>
    /// ScriptableObject catalog defining properties for each stellar class.
    /// Shared catalog that games can extend with additional classes or modify values.
    /// </summary>
    [CreateAssetMenu(fileName = "StellarClassCatalog", menuName = "PureDOTS/Space/Stellar Class Catalog", order = 100)]
    public class StellarClassCatalog : ScriptableObject
    {
        [Header("Main Sequence Stars")]
        [SerializeField, Tooltip("O-type star properties (very hot, blue, massive).")]
        private StellarClassProperties oType = new StellarClassProperties
        {
            TypicalMass = 60f,
            TypicalLuminosity = 1000000f,
            TypicalTemperature = 30000f,
            MinMass = 16f,
            MaxMass = 100f,
            MinLuminosity = 30000f,
            MaxLuminosity = 1000000f,
            MinTemperature = 30000f,
            MaxTemperature = 50000f
        };

        [SerializeField, Tooltip("B-type star properties (hot, blue-white).")]
        private StellarClassProperties bType = new StellarClassProperties
        {
            TypicalMass = 10f,
            TypicalLuminosity = 25000f,
            TypicalTemperature = 20000f,
            MinMass = 2.1f,
            MaxMass = 16f,
            MinLuminosity = 25f,
            MaxLuminosity = 30000f,
            MinTemperature = 10000f,
            MaxTemperature = 30000f
        };

        [SerializeField, Tooltip("A-type star properties (white, hot).")]
        private StellarClassProperties aType = new StellarClassProperties
        {
            TypicalMass = 2.5f,
            TypicalLuminosity = 20f,
            TypicalTemperature = 8500f,
            MinMass = 1.4f,
            MaxMass = 2.1f,
            MinLuminosity = 5f,
            MaxLuminosity = 25f,
            MinTemperature = 7500f,
            MaxTemperature = 10000f
        };

        [SerializeField, Tooltip("F-type star properties (white-yellow).")]
        private StellarClassProperties fType = new StellarClassProperties
        {
            TypicalMass = 1.3f,
            TypicalLuminosity = 4f,
            TypicalTemperature = 6500f,
            MinMass = 1.04f,
            MaxMass = 1.4f,
            MinLuminosity = 1.5f,
            MaxLuminosity = 5f,
            MinTemperature = 6000f,
            MaxTemperature = 7500f
        };

        [SerializeField, Tooltip("G-type star properties (yellow, like our Sun).")]
        private StellarClassProperties gType = new StellarClassProperties
        {
            TypicalMass = 1.0f,
            TypicalLuminosity = 1.0f,
            TypicalTemperature = 5778f,
            MinMass = 0.8f,
            MaxMass = 1.04f,
            MinLuminosity = 0.6f,
            MaxLuminosity = 1.5f,
            MinTemperature = 5200f,
            MaxTemperature = 6000f
        };

        [SerializeField, Tooltip("K-type star properties (orange).")]
        private StellarClassProperties kType = new StellarClassProperties
        {
            TypicalMass = 0.7f,
            TypicalLuminosity = 0.4f,
            TypicalTemperature = 4000f,
            MinMass = 0.45f,
            MaxMass = 0.8f,
            MinLuminosity = 0.08f,
            MaxLuminosity = 0.6f,
            MinTemperature = 3700f,
            MaxTemperature = 5200f
        };

        [SerializeField, Tooltip("M-type star properties (red dwarf, most common).")]
        private StellarClassProperties mType = new StellarClassProperties
        {
            TypicalMass = 0.3f,
            TypicalLuminosity = 0.04f,
            TypicalTemperature = 3000f,
            MinMass = 0.08f,
            MaxMass = 0.45f,
            MinLuminosity = 0.0001f,
            MaxLuminosity = 0.08f,
            MinTemperature = 2400f,
            MaxTemperature = 3700f
        };

        [Header("Special Stars")]
        [SerializeField, Tooltip("White dwarf properties (small, dense remnant).")]
        private StellarClassProperties whiteDwarf = new StellarClassProperties
        {
            TypicalMass = 0.6f,
            TypicalLuminosity = 0.0001f,
            TypicalTemperature = 10000f,
            MinMass = 0.1f,
            MaxMass = 1.4f,
            MinLuminosity = 0.00001f,
            MaxLuminosity = 0.001f,
            MinTemperature = 4000f,
            MaxTemperature = 150000f
        };

        [SerializeField, Tooltip("Brown dwarf properties (failed star).")]
        private StellarClassProperties brownDwarf = new StellarClassProperties
        {
            TypicalMass = 0.05f,
            TypicalLuminosity = 0.00001f,
            TypicalTemperature = 2000f,
            MinMass = 0.01f,
            MaxMass = 0.08f,
            MinLuminosity = 0.000001f,
            MaxLuminosity = 0.0001f,
            MinTemperature = 300f,
            MaxTemperature = 3000f
        };

        [SerializeField, Tooltip("Black hole properties (collapsed star).")]
        private StellarClassProperties blackHole = new StellarClassProperties
        {
            TypicalMass = 10f,
            TypicalLuminosity = 0f, // Black holes don't emit light
            TypicalTemperature = 0f,
            MinMass = 3f,
            MaxMass = 1000f,
            MinLuminosity = 0f,
            MaxLuminosity = 0f,
            MinTemperature = 0f,
            MaxTemperature = 0f
        };

        /// <summary>
        /// Get properties for a stellar class.
        /// </summary>
        public StellarClassProperties GetProperties(StellarClass stellarClass)
        {
            return stellarClass switch
            {
                StellarClass.O => oType,
                StellarClass.B => bType,
                StellarClass.A => aType,
                StellarClass.F => fType,
                StellarClass.G => gType,
                StellarClass.K => kType,
                StellarClass.M => mType,
                StellarClass.WhiteDwarf => whiteDwarf,
                StellarClass.BrownDwarf => brownDwarf,
                StellarClass.BlackHole => blackHole,
                _ => gType // Default to G-type (Sun-like)
            };
        }

        /// <summary>
        /// Get typical mass for a stellar class.
        /// </summary>
        public float GetTypicalMass(StellarClass stellarClass)
        {
            return GetProperties(stellarClass).TypicalMass;
        }

        /// <summary>
        /// Get typical luminosity for a stellar class.
        /// </summary>
        public float GetTypicalLuminosity(StellarClass stellarClass)
        {
            return GetProperties(stellarClass).TypicalLuminosity;
        }

        /// <summary>
        /// Get typical temperature for a stellar class.
        /// </summary>
        public float GetTypicalTemperature(StellarClass stellarClass)
        {
            return GetProperties(stellarClass).TypicalTemperature;
        }
    }
}
#endif
























