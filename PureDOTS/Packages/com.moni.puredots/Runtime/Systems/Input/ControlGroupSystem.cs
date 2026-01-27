using PureDOTS.Input;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Systems.Input
{
    /// <summary>
    /// Processes control group input events (Ctrl+Number save, Number recall).
    /// Manages ControlGroupState singleton storing 10 control groups with members and camera bookmarks.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SelectionSystem))]
    public partial struct ControlGroupSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RtsInputSingletonTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Entity rtsInputEntity = SystemAPI.GetSingletonEntity<RtsInputSingletonTag>();

            if (!state.EntityManager.HasBuffer<ControlGroupInputEvent>(rtsInputEntity))
            {
                return;
            }

            // Ensure ControlGroupState singleton exists
            Entity controlGroupEntity;
            if (!SystemAPI.TryGetSingletonEntity<ControlGroupState>(out controlGroupEntity))
            {
                controlGroupEntity = state.EntityManager.CreateEntity(typeof(ControlGroupState));
                var initialState = new ControlGroupState
                {
                    Groups = new Unity.Collections.FixedList128Bytes<ControlGroup>()
                };
                state.EntityManager.SetComponentData(controlGroupEntity, initialState);
            }

            var groupState = state.EntityManager.GetComponentData<ControlGroupState>(controlGroupEntity);
            var controlGroupBuffer = state.EntityManager.GetBuffer<ControlGroupInputEvent>(rtsInputEntity);

            for (int i = 0; i < controlGroupBuffer.Length; i++)
            {
                var inputEvent = controlGroupBuffer[i];
                ProcessControlGroupInput(ref state, rtsInputEntity, ref groupState, inputEvent);
            }

            state.EntityManager.SetComponentData(controlGroupEntity, groupState);
            controlGroupBuffer.Clear();
        }

        private void ProcessControlGroupInput(ref SystemState state, Entity rtsInputEntity, ref ControlGroupState groupState, ControlGroupInputEvent inputEvent)
        {
            int index = inputEvent.Number;
            if (index < 0 || index > 9)
            {
                return; // Invalid group number
            }

            if (inputEvent.Save != 0)
            {
                // Save operation
                SaveControlGroup(ref state, ref groupState, index, inputEvent.Additive != 0, inputEvent.PlayerId);
            }
            else if (inputEvent.Recall != 0)
            {
                // Recall operation
                RecallControlGroup(ref state, rtsInputEntity, ref groupState, index, inputEvent.PlayerId);
            }
        }

        private void SaveControlGroup(ref SystemState state, ref ControlGroupState groupState, int index, bool additive, byte playerId)
        {
            // Get current selection
            var selectedEntities = new NativeList<Entity>(Allocator.Temp);
            foreach (var (_, owner, entity) in SystemAPI.Query<SelectedTag, SelectionOwner>()
                         .WithEntityAccess())
            {
                if (owner.PlayerId == playerId)
                {
                    selectedEntities.Add(entity);
                }
            }

            // Get or create group at index
            var groups = groupState.Groups;
            ControlGroup group = index < groups.Length ? groups[index] : new ControlGroup();

            if (selectedEntities.Length > 0)
            {
                // Save selection as group members
                if (!additive)
                {
                    group.Members.Clear();
                    group.HasMembers = false;
                }

                foreach (var entity in selectedEntities)
                {
                    if (!group.Members.Contains(entity) && group.Members.Length < 64)
                    {
                        group.Members.Add(entity);
                        group.HasMembers = true;
                    }
                }
            }
            else
            {
                // Save camera bookmark if no selection
                Camera camera = Camera.main;
                if (camera != null && !additive)
                {
                    group.BookmarkPosition = new float3(camera.transform.position.x, camera.transform.position.y, camera.transform.position.z);
                    group.BookmarkRotation = new quaternion(camera.transform.rotation.x, camera.transform.rotation.y, camera.transform.rotation.z, camera.transform.rotation.w);
                    group.HasCameraBookmark = true;
                }
            }

            // Update group in list
            if (index >= groups.Length)
            {
                // Extend list if needed
                while (groups.Length <= index)
                {
                    groups.Add(new ControlGroup());
                }
            }
            groups[index] = group;
            groupState.Groups = groups;

            selectedEntities.Dispose();
        }

        private void RecallControlGroup(ref SystemState state, Entity rtsInputEntity, ref ControlGroupState groupState, int index, byte playerId)
        {
            if (index < 0 || index >= groupState.Groups.Length)
            {
                return;
            }

            var group = groupState.Groups[index];

            if (group.HasMembers)
            {
                // Recall selection
                // Clear current selection for this player
                var ecb = new EntityCommandBuffer(Allocator.Temp);
                foreach (var (_, owner, entity) in SystemAPI.Query<SelectedTag, SelectionOwner>()
                             .WithEntityAccess())
                {
                    if (owner.PlayerId == playerId)
                    {
                        ecb.RemoveComponent<SelectedTag>(entity);
                    }
                }
                ecb.Playback(state.EntityManager);
                ecb.Dispose();

                // Apply selection to group members
                foreach (var member in group.Members)
                {
                    if (state.EntityManager.Exists(member))
                    {
                        state.EntityManager.AddComponent<SelectedTag>(member);
                    }
                }
            }
            else if (group.HasCameraBookmark)
            {
                // Emit a request for the active game camera rig to consume (keeps the camera contract single-writer).
                if (state.EntityManager.HasBuffer<CameraRequestEvent>(rtsInputEntity))
                {
                    var requests = state.EntityManager.GetBuffer<CameraRequestEvent>(rtsInputEntity);
                    requests.Add(new CameraRequestEvent
                    {
                        Kind = CameraRequestKind.RecallBookmark,
                        BookmarkPosition = group.BookmarkPosition,
                        BookmarkRotation = group.BookmarkRotation,
                        PlayerId = playerId
                    });
                }
            }
        }
    }
}
