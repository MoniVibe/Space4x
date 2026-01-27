using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Environment
{
    /// <summary>
    /// Type of light source.
    /// </summary>
    public enum LightSourceType : byte
    {
        Celestial = 0,      // Star, sun
        Artificial = 1,     // Lamps, fires
        Magical = 2,        // Spells, enchantments
        Bioluminescent = 3, // Glowing creatures/plants
        Reflected = 4       // Moon light, albedo
    }

    /// <summary>
    /// Light attenuation model.
    /// </summary>
    public enum LightAttenuation : byte
    {
        None = 0,           // Infinite range (directional)
        Linear = 1,         // Falls off linearly
        Quadratic = 2,      // Inverse square law
        Exponential = 3     // Rapid falloff
    }

    /// <summary>
    /// A light source entity that emits light.
    /// </summary>
    public struct LightSource : IComponentData
    {
        public LightSourceType SourceType;
        public LightAttenuation Attenuation;
        public float Intensity;                 // Base intensity 0-100
        public float Range;                     // Maximum range
        public float3 Color;                    // RGB 0-1
        public float3 Direction;                // For directional lights
        public float ConeAngle;                 // For spotlights (radians)
        public float InnerConeAngle;            // Inner spotlight cone
        public byte IsDirectional;              // 0 = point, 1 = directional
        public byte CastsShadows;
        public byte IsActive;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Dynamic light state for flickering/animated lights.
    /// </summary>
    public struct LightDynamics : IComponentData
    {
        public float BaseIntensity;
        public float FlickerAmount;             // 0-1 random variation
        public float FlickerSpeed;              // Variations per second
        public float PulseAmount;               // 0-1 sine wave variation
        public float PulseSpeed;                // Cycles per second
        public float CurrentIntensity;          // After dynamics applied
        public uint RandomSeed;
    }

    /// <summary>
    /// Light contributions from multiple sources on an entity.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct LightSourceInfluence : IBufferElementData
    {
        public Entity SourceEntity;
        public LightSourceType SourceType;
        public float Intensity;                 // Contribution from this source
        public float3 Direction;                // Direction to source
        public float Distance;                  // Distance to source
        public float ShadowFactor;              // 0 = full shadow, 1 = no shadow
    }

    /// <summary>
    /// Accumulated light exposure on an entity.
    /// </summary>
    public struct LightExposure : IComponentData
    {
        public float TotalIntensity;            // Sum of all sources
        public float DirectIntensity;           // Direct (non-ambient) light
        public float AmbientIntensity;          // Ambient light only
        public float3 DominantDirection;        // Direction of strongest source
        public float3 AccumulatedColor;         // Weighted average color
        public uint LastUpdateTick;
        public byte InShadow;                   // In shadow of any occluder
        public byte InFullDarkness;             // Below minimum threshold
    }

    /// <summary>
    /// Global ambient light state.
    /// </summary>
    public struct AmbientLightState : IComponentData
    {
        public float SkyIntensity;              // From sky/atmosphere
        public float GroundIntensity;           // From ground bounce
        public float3 SkyColor;
        public float3 GroundColor;
        public float3 HorizonColor;
        public float FogDensity;                // Affects light scatter
        public float FogDistance;               // Fog visibility range
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Light grid for spatial light queries.
    /// </summary>
    public struct LightGrid : IComponentData
    {
        public float3 WorldMin;
        public float3 WorldMax;
        public float CellSize;
        public int2 Resolution;
        public uint LastUpdateTick;
        public uint Version;
    }

    /// <summary>
    /// Per-cell light sample in the light grid.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct LightGridCell : IBufferElementData
    {
        public float DirectLight;
        public float AmbientLight;
        public float3 DominantDirection;
        public byte SourceCount;
    }

    /// <summary>
    /// Light occluder that blocks light.
    /// </summary>
    public struct LightOccluder : IComponentData
    {
        public float Opacity;                   // 0 = transparent, 1 = opaque
        public float3 Size;                     // Bounding box
        public float ShadowSoftness;            // Penumbra size
        public byte CastsStaticShadow;
        public byte CastsDynamicShadow;
    }

    /// <summary>
    /// Shadow volume cast by an occluder.
    /// </summary>
    public struct ShadowVolume : IComponentData
    {
        public Entity OccluderEntity;
        public Entity LightSourceEntity;
        public float3 Direction;                // Shadow direction
        public float Length;                    // Shadow reach
        public float Width;                     // Shadow width at source
        public float Falloff;                   // Penumbra softness
    }

    /// <summary>
    /// Light registry for efficient queries.
    /// </summary>
    public struct LightSourceRegistry : IComponentData
    {
        public int ActiveLightCount;
        public int StaticLightCount;
        public int DynamicLightCount;
        public float GlobalIntensityMultiplier;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Entry in light source registry.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct LightSourceEntry : IBufferElementData
    {
        public Entity Entity;
        public LightSourceType SourceType;
        public float3 Position;
        public float Intensity;
        public float Range;
        public byte IsActive;
        public byte IsStatic;
    }

    /// <summary>
    /// Time-of-day light modifiers.
    /// </summary>
    public struct TimeOfDayLighting : IComponentData
    {
        public float SunIntensity;
        public float MoonIntensity;
        public float3 SunColor;
        public float3 MoonColor;
        public float3 SkyColorDay;
        public float3 SkyColorNight;
        public float3 SkyColorDawn;
        public float3 SkyColorDusk;
        public float TransitionProgress;        // 0-1 for color blending
    }
}

