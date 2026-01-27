#if PUREDOTS_SCENARIO && PUREDOTS_LEGACY_SCENARIO_ASM

using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Scripting.APIUpdating;

namespace PureDOTS.LegacyScenario.Orbit
{
    /// <summary>Tag for legacy scenario orbit cubes.</summary>
    [MovedFrom(true, "PureDOTS.Demo.Orbit", null, "OrbitCubeTag")]
    public struct OrbitCubeTag : IComponentData { }

    /// <summary>Simple orbital motion parameters.</summary>
    [MovedFrom(true, "PureDOTS.Demo.Orbit", null, "OrbitCube")]
    public struct OrbitCube : IComponentData
    {
        public float3 Center;       // Center point to orbit around
        public float Radius;        // Distance from center
        public float AngularSpeed;  // radians per second
        public float Angle;         // current angle in radians
    }
}

#endif
