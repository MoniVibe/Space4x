using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Presentation;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Copies authoritative transforms into presentation companions so bridge visuals follow targets.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    public partial struct CompanionPresentationSyncSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;

        public void OnCreate(ref SystemState state)
        {
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            // Skip during rewind playback (visuals are regenerated or restored).
            if (SystemAPI.TryGetSingleton<RewindState>(out var rewind) && rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var bridge = PresentationBridgeLocator.TryResolve();
            if (bridge == null)
            {
                return;
            }
            _transformLookup.Update(ref state);

            foreach (var (companion, entity) in SystemAPI.Query<RefRO<CompanionPresentation>>().WithEntityAccess())
            {
                if (!_transformLookup.HasComponent(entity))
                {
                    continue;
                }

                if (!bridge.TryGetInstance(companion.ValueRO.Handle, out var instance))
                {
                    continue;
                }

                var targetTransform = _transformLookup[entity];
                float followLerp = math.saturate(companion.ValueRO.FollowLerp);
                float sizeScale = companion.ValueRO.Style.Size > 0f ? companion.ValueRO.Style.Size : 1f;
                float appliedScale = math.max(0.01f, targetTransform.Scale * sizeScale);

                var pos = targetTransform.Position + math.rotate(targetTransform.Rotation, companion.ValueRO.Offset);
                var rot = targetTransform.Rotation;

                Transform target = instance.transform;

                if (followLerp > 0f)
                {
                    var lerpPos = math.lerp(new float3(target.position.x, target.position.y, target.position.z), pos, followLerp);
                    target.position = new Vector3(lerpPos.x, lerpPos.y, lerpPos.z);
                    var currentRot = new quaternion(target.rotation.x, target.rotation.y, target.rotation.z, target.rotation.w);
                    var lerpRot = math.slerp(currentRot, rot, followLerp);
                    target.rotation = new Quaternion(lerpRot.value.x, lerpRot.value.y, lerpRot.value.z, lerpRot.value.w);
                }
                else
                {
                    target.position = new Vector3(pos.x, pos.y, pos.z);
                    target.rotation = new Quaternion(rot.value.x, rot.value.y, rot.value.z, rot.value.w);
                }

                target.localScale = new Vector3(appliedScale, appliedScale, appliedScale);
            }
        }
    }
}
