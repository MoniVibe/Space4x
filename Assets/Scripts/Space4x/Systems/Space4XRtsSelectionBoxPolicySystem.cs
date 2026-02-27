using PureDOTS.Input;
using PureDOTS.Runtime.Core;
using PureDOTS.Systems.Input;
using Space4X.UI;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Systems
{
    /// <summary>
    /// Space4X RTS box-selection policy:
    /// if a drag hits any owned units, select only owned; otherwise select foreign units for inspection.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(SelectionSystem))]
    public partial struct Space4XRtsSelectionBoxPolicySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RtsInputSingletonTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled || Space4XControlModeState.CurrentMode != Space4XControlMode.Rts)
            {
                return;
            }

            var inputEntity = SystemAPI.GetSingletonEntity<RtsInputSingletonTag>();
            if (!state.EntityManager.HasBuffer<SelectionBoxEvent>(inputEntity))
            {
                return;
            }

            var boxBuffer = state.EntityManager.GetBuffer<SelectionBoxEvent>(inputEntity);
            if (boxBuffer.Length == 0)
            {
                return;
            }

            using var boxEvents = boxBuffer.ToNativeArray(Allocator.Temp);
            boxBuffer.Clear();

            var camera = UnityEngine.Camera.main ?? UnityEngine.Object.FindAnyObjectByType<UnityEngine.Camera>();
            if (camera == null)
            {
                return;
            }

            foreach (var boxEvent in boxEvents)
            {
                ProcessBoxEvent(ref state, camera, boxEvent);
            }
        }

        private void ProcessBoxEvent(ref SystemState state, UnityEngine.Camera camera, SelectionBoxEvent boxEvent)
        {
            float minX = Mathf.Min(boxEvent.ScreenMin.x, boxEvent.ScreenMax.x);
            float maxX = Mathf.Max(boxEvent.ScreenMin.x, boxEvent.ScreenMax.x);
            float minY = Mathf.Min(boxEvent.ScreenMin.y, boxEvent.ScreenMax.y);
            float maxY = Mathf.Max(boxEvent.ScreenMin.y, boxEvent.ScreenMax.y);

            if (maxX - minX <= 0.1f || maxY - minY <= 0.1f)
            {
                return;
            }

            var ownedHits = new NativeList<Entity>(Allocator.Temp);
            var foreignHits = new NativeList<Entity>(Allocator.Temp);

            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<SelectableTag>().WithEntityAccess())
            {
                var worldPosition = transform.ValueRO.Position;
                var screenPoint = camera.WorldToScreenPoint(new Vector3(worldPosition.x, worldPosition.y, worldPosition.z));
                if (screenPoint.z <= 0f)
                {
                    continue;
                }

                if (screenPoint.x < minX || screenPoint.x > maxX || screenPoint.y < minY || screenPoint.y > maxY)
                {
                    continue;
                }

                byte ownerId = byte.MaxValue;
                if (state.EntityManager.HasComponent<SelectionOwner>(entity))
                {
                    ownerId = state.EntityManager.GetComponentData<SelectionOwner>(entity).PlayerId;
                }

                if (ownerId == boxEvent.PlayerId)
                {
                    ownedHits.Add(entity);
                }
                else
                {
                    foreignHits.Add(entity);
                }
            }

            var selectionSet = ownedHits.Length > 0 ? ownedHits : foreignHits;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            if (boxEvent.Mode == SelectionBoxMode.Replace)
            {
                ClearAllSelected(ref state, ref ecb);
                for (int i = 0; i < selectionSet.Length; i++)
                {
                    ecb.AddComponent<SelectedTag>(selectionSet[i]);
                }
            }
            else
            {
                for (int i = 0; i < selectionSet.Length; i++)
                {
                    var entity = selectionSet[i];
                    if (state.EntityManager.HasComponent<SelectedTag>(entity))
                    {
                        ecb.RemoveComponent<SelectedTag>(entity);
                    }
                    else
                    {
                        ecb.AddComponent<SelectedTag>(entity);
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            ownedHits.Dispose();
            foreignHits.Dispose();
        }

        private void ClearAllSelected(ref SystemState state, ref EntityCommandBuffer ecb)
        {
            var selectedEntities = new NativeList<Entity>(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<SelectedTag>().WithEntityAccess())
            {
                selectedEntities.Add(entity);
            }

            for (int i = 0; i < selectedEntities.Length; i++)
            {
                ecb.RemoveComponent<SelectedTag>(selectedEntities[i]);
            }

            selectedEntities.Dispose();
        }
    }
}
