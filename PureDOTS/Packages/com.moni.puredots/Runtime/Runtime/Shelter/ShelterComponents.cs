using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Shelter
{
    /// <summary>
    /// Type of occluder providing shelter.
    /// </summary>
    public enum OccluderType : byte
    {
        Terrain = 0,        // Hills, mountains, cliffs
        Structure = 1,      // Buildings, walls
        Asteroid = 2,       // Space rocks
        Vegetation = 3,     // Trees, dense foliage
        Vehicle = 4,        // Ships, transports
        Shield = 5,         // Energy shields
        Artificial = 6      // Constructed barriers
    }

    /// <summary>
    /// Type of hazard to shelter from.
    /// </summary>
    public enum HazardSourceType : byte
    {
        Radiation = 0,      // Radiation from star/zone
        Solar = 1,          // Direct sunlight (heat)
        Weather = 2,        // Rain, storm, snow
        Wind = 3,           // Strong wind
        Projectile = 4,     // Combat/debris
        Magical = 5         // Magical effects
    }

    /// <summary>
    /// Entity that provides shelter to others.
    /// </summary>
    public struct ShelterProvider : IComponentData
    {
        public OccluderType OccluderType;
        public float CoverageRadius;            // How far shelter extends
        public float CoverageHeight;            // Vertical coverage
        public float CoverageAngle;             // Angular coverage (radians)
        public float3 CoverageDirection;        // Primary protection direction
        public float Opacity;                   // 0-1 how much blocks
        public float Durability;                // Structural integrity
        public byte ProvidesFull360;            // Shelter from all directions
        public byte ProvidesOverhead;           // Shelter from above
    }

    /// <summary>
    /// Entity that seeks shelter from hazards.
    /// </summary>
    public struct ShelterSeeker : IComponentData
    {
        public HazardSourceType SeekingFrom;    // What hazard to avoid
        public float SeekRadius;                // How far to look
        public float MinShelterLevel;           // Minimum acceptable
        public float PreferenceWeight;          // How much to prioritize
        public byte IsCurrentlySeeking;
        public byte CanMoveToShelter;           // Mobility
    }

    /// <summary>
    /// Current shelter status for an entity.
    /// </summary>
    public struct ShelterState : IComponentData
    {
        public Entity ShelterProvider;          // What's providing shelter
        public float ShelterLevel;              // 0-1 protection level
        public float3 ShelterDirection;         // Direction of protection
        public HazardSourceType ProtectedFrom;
        public uint EnteredTick;
        public byte IsFullyCovered;             // Complete protection
        public byte IsPartialCover;             // Partial protection
    }

    /// <summary>
    /// Occlusion data for shadow/shelter calculations.
    /// </summary>
    public struct OcclusionData : IComponentData
    {
        public float3 Position;
        public float3 Size;                     // Bounding box
        public float Radius;                    // For spherical
        public float Height;                    // For cylindrical
        public byte IsConvex;                   // Simplified collision
        public byte CastsShadow;
    }

    /// <summary>
    /// Grid-based occlusion map for efficient queries.
    /// </summary>
    public struct OcclusionMap : IComponentData
    {
        public float3 WorldMin;
        public float3 WorldMax;
        public float CellSize;
        public int2 Resolution;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Per-cell occlusion data.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct OcclusionCell : IBufferElementData
    {
        public float ShelterLevel;              // 0-1 ground coverage
        public float OverheadCover;             // 0-1 overhead coverage
        public byte OccluderCount;              // Number of occluders
        public byte HasStructure;
        public byte HasVegetation;
    }

    /// <summary>
    /// Shelter query request.
    /// </summary>
    public struct ShelterQuery : IComponentData
    {
        public float3 FromPosition;
        public float3 HazardDirection;          // Direction hazard comes from
        public float QueryRadius;
        public HazardSourceType HazardType;
        public byte RequiresFullCover;
        public byte RequiresOverhead;
    }

    /// <summary>
    /// Result of a shelter query.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ShelterQueryResult : IBufferElementData
    {
        public Entity ProviderEntity;
        public float3 ShelterPosition;          // Best position for cover
        public float ShelterLevel;
        public float Distance;
        public byte MeetsCriteria;
    }

    /// <summary>
    /// Line-of-sight blocking between points.
    /// </summary>
    public struct LineOfSightBlock : IComponentData
    {
        public Entity OccluderEntity;
        public float3 BlockStart;
        public float3 BlockEnd;
        public float BlockFactor;               // 0-1 how much blocks
    }

    /// <summary>
    /// Cover position for tactical use.
    /// </summary>
    public struct CoverPosition : IComponentData
    {
        public float3 Position;
        public float3 CoverDirection;           // Direction protected
        public float CoverQuality;              // 0-1 how good
        public float Height;                    // Cover height
        public Entity ProviderEntity;
        public byte IsOccupied;
        public byte AllowsFiring;               // Can attack while covered
    }

    /// <summary>
    /// Registry of shelter providers.
    /// </summary>
    public struct ShelterRegistry : IComponentData
    {
        public int ProviderCount;
        public int StructureCount;
        public int NaturalCount;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Entry in shelter registry.
    /// </summary>
    [InternalBufferCapacity(64)]
    public struct ShelterEntry : IBufferElementData
    {
        public Entity Entity;
        public OccluderType Type;
        public float3 Position;
        public float CoverageRadius;
        public float ShelterQuality;
        public byte IsActive;
    }

    /// <summary>
    /// Directional cover analysis.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct DirectionalCover : IBufferElementData
    {
        public float3 Direction;                // Cardinal/ordinal direction
        public float CoverLevel;                // 0-1 protection from this direction
        public Entity NearestProvider;
        public float Distance;
    }

    /// <summary>
    /// Dynamic shelter that can be created/destroyed.
    /// </summary>
    public struct DynamicShelter : IComponentData
    {
        public float BuildProgress;             // 0-1 construction
        public float MaxDurability;
        public float CurrentDurability;
        public uint CreatedTick;
        public uint ExpirationTick;             // 0 = permanent
        public byte IsTemporary;
        public byte CanBeRepaired;
    }
}

