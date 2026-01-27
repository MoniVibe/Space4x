using Unity.Burst;
using Unity.Mathematics;
using System.Runtime.CompilerServices;

namespace PureDOTS.Runtime.Movement
{
    /// <summary>
    /// Burst-compatible utility functions for working with orientations in 3D space.
    /// Use these instead of hardcoding math.up(), math.forward(), etc.
    /// </summary>
    [BurstCompile]
    public static class OrientationHelpers
    {
        /// <summary>
        /// World up vector constant. Use this when you explicitly need world up
        /// (e.g., UI, camera horizon, gravity direction).
        /// </summary>
        public static readonly float3 WorldUp = new float3(0, 1, 0);
        
        /// <summary>
        /// World forward vector constant.
        /// </summary>
        public static readonly float3 WorldForward = new float3(0, 0, 1);
        
        /// <summary>
        /// World right vector constant.
        /// </summary>
        public static readonly float3 WorldRight = new float3(1, 0, 0);

        /// <summary>
        /// Extracts the up vector from a quaternion rotation.
        /// Equivalent to rotating (0, 1, 0) by the quaternion.
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetUpVector(in quaternion rotation, out float3 result)
        {
            result = math.mul(rotation, new float3(0, 1, 0));
        }

        /// <summary>
        /// Extracts the forward vector from a quaternion rotation.
        /// Equivalent to rotating (0, 0, 1) by the quaternion.
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetForwardVector(in quaternion rotation, out float3 result)
        {
            result = math.mul(rotation, new float3(0, 0, 1));
        }

        /// <summary>
        /// Extracts the right vector from a quaternion rotation.
        /// Equivalent to rotating (1, 0, 0) by the quaternion.
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetRightVector(in quaternion rotation, out float3 result)
        {
            result = math.mul(rotation, new float3(1, 0, 0));
        }

        /// <summary>
        /// Creates a look rotation with a smart up vector derivation.
        /// If the forward direction is nearly parallel to the hint up, uses an alternative.
        /// </summary>
        /// <param name="forward">The direction to look at (will be normalized).</param>
        /// <param name="upHint">The preferred up direction.</param>
        /// <param name="result">A quaternion rotation facing the forward direction.</param>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LookRotationSafe3D(in float3 forward, in float3 upHint, out quaternion result)
        {
            float forwardLengthSq = math.lengthsq(forward);
            if (forwardLengthSq < 1e-10f)
            {
                result = quaternion.identity;
                return;
            }

            float3 fwd = math.normalize(forward);
            
            // Check if forward is nearly parallel to up hint
            float dot = math.abs(math.dot(fwd, upHint));
            if (dot > 0.9999f)
            {
                // Forward is parallel to up, use an alternative up
                // Choose the axis that's most perpendicular to forward
                float3 altUp = math.abs(fwd.x) < 0.9f 
                    ? new float3(1, 0, 0) 
                    : new float3(0, 1, 0);
                result = quaternion.LookRotationSafe(fwd, altUp);
                return;
            }

            result = quaternion.LookRotationSafe(fwd, upHint);
        }

        /// <summary>
        /// Derives an appropriate up vector from the current rotation.
        /// Useful when you need to maintain orientation consistency.
        /// </summary>
        /// <param name="currentRotation">The entity's current rotation.</param>
        /// <param name="fallbackUp">Fallback up vector if derivation fails.</param>
        /// <param name="result">The derived up vector.</param>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DeriveUpFromRotation(in quaternion currentRotation, in float3 fallbackUp, out float3 result)
        {
            GetUpVector(currentRotation, out float3 up);
            float lengthSq = math.lengthsq(up);
            
            if (lengthSq < 1e-10f)
            {
                result = fallbackUp;
                return;
            }
            
            result = math.normalize(up);
        }

        /// <summary>
        /// Creates a rotation that looks in the given direction while trying to maintain
        /// the current roll (rotation around forward axis).
        /// </summary>
        /// <param name="forward">The new forward direction.</param>
        /// <param name="currentRotation">The current rotation to preserve roll from.</param>
        /// <param name="result">A new rotation facing forward with preserved roll.</param>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LookRotationPreserveRoll(in float3 forward, in quaternion currentRotation, out quaternion result)
        {
            float forwardLengthSq = math.lengthsq(forward);
            if (forwardLengthSq < 1e-10f)
            {
                result = currentRotation;
                return;
            }

            float3 fwd = math.normalize(forward);
            GetUpVector(currentRotation, out float3 currentUp);
            
            LookRotationSafe3D(fwd, currentUp, out result);
        }

        /// <summary>
        /// Computes an orthonormal basis (right, up, forward) from a forward direction
        /// and an up hint. Useful for weapon spread, formation layouts, etc.
        /// </summary>
        /// <param name="forward">The forward direction (will be normalized).</param>
        /// <param name="upHint">The preferred up direction.</param>
        /// <param name="right">Output right vector.</param>
        /// <param name="up">Output up vector (orthogonal to forward).</param>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ComputeOrthonormalBasis(in float3 forward, in float3 upHint, out float3 right, out float3 up)
        {
            float forwardLengthSq = math.lengthsq(forward);
            if (forwardLengthSq < 1e-10f)
            {
                right = new float3(1, 0, 0);
                up = new float3(0, 1, 0);
                return;
            }

            float3 fwd = math.normalize(forward);
            float3 hint = upHint;
            
            // Check if forward is nearly parallel to up hint
            float dot = math.abs(math.dot(fwd, hint));
            if (dot > 0.9999f)
            {
                // Use alternative up
                hint = math.abs(fwd.x) < 0.9f 
                    ? new float3(1, 0, 0) 
                    : new float3(0, 1, 0);
            }

            right = math.normalize(math.cross(hint, fwd));
            up = math.cross(fwd, right);
        }

        /// <summary>
        /// Extracts yaw angle (rotation around Y axis) from a quaternion.
        /// Useful for ground units that need to constrain to yaw-only rotation.
        /// </summary>
        /// <param name="rotation">The rotation to extract yaw from.</param>
        /// <param name="result">Yaw angle in radians.</param>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ExtractYaw(in quaternion rotation, out float result)
        {
            GetForwardVector(rotation, out float3 forward);
            result = math.atan2(forward.x, forward.z);
        }

        /// <summary>
        /// Creates a yaw-only rotation (around world Y axis).
        /// Useful for ground units that should stay upright.
        /// </summary>
        /// <param name="yawRadians">Yaw angle in radians.</param>
        /// <param name="result">A quaternion rotation around the Y axis.</param>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void YawOnlyRotation(float yawRadians, out quaternion result)
        {
            result = quaternion.RotateY(yawRadians);
        }

        /// <summary>
        /// Constrains a rotation to yaw-only (removes pitch and roll).
        /// Useful for ground units that should stay upright.
        /// </summary>
        /// <param name="rotation">The rotation to constrain.</param>
        /// <param name="result">A yaw-only rotation.</param>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConstrainToYawOnly(in quaternion rotation, out quaternion result)
        {
            ExtractYaw(rotation, out float yaw);
            YawOnlyRotation(yaw, out result);
        }

        /// <summary>
        /// Creates a rotation aligned to a surface normal while facing a direction.
        /// Useful for ground units on sloped terrain.
        /// </summary>
        /// <param name="forward">The desired forward direction (projected onto surface).</param>
        /// <param name="surfaceNormal">The surface normal to align up to.</param>
        /// <param name="result">A rotation aligned to the surface.</param>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AlignToSurface(in float3 forward, in float3 surfaceNormal, out quaternion result)
        {
            // Project forward onto the surface plane
            float3 projectedForward = forward - surfaceNormal * math.dot(forward, surfaceNormal);
            float lengthSq = math.lengthsq(projectedForward);
            
            if (lengthSq < 1e-10f)
            {
                // Forward is perpendicular to surface, use world forward projected
                projectedForward = new float3(0, 0, 1) - surfaceNormal * math.dot(new float3(0, 0, 1), surfaceNormal);
                lengthSq = math.lengthsq(projectedForward);
                
                if (lengthSq < 1e-10f)
                {
                    // Surface is horizontal, use world forward
                    projectedForward = new float3(0, 0, 1);
                }
            }
            
            projectedForward = math.normalize(projectedForward);
            result = quaternion.LookRotationSafe(projectedForward, surfaceNormal);
        }
    }
}

