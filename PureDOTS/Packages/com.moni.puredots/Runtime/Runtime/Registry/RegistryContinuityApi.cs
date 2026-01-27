using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Registry
{
    /// <summary>
    /// Configuration for registering a custom registry with continuity validation.
    /// </summary>
    public struct RegistryContinuityRegistration
    {
        /// <summary>
        /// Human-readable label for the registry (e.g., "Space4X.Colonies").
        /// </summary>
        public FixedString64Bytes Label;

        /// <summary>
        /// Whether the registry requires spatial grid synchronization.
        /// </summary>
        public bool RequiresSpatialSync;

        /// <summary>
        /// Whether the registry supports rewind operations.
        /// </summary>
        public bool SupportsRewind;

        /// <summary>
        /// Initial continuity snapshot for the registry.
        /// </summary>
        public RegistryContinuitySnapshot Snapshot;

        /// <summary>
        /// The tick when this registry was last updated.
        /// </summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Handle returned from registry participant registration for subsequent updates.
    /// </summary>
    public struct RegistryContinuityParticipantHandle : IEquatable<RegistryContinuityParticipantHandle>
    {
        /// <summary>
        /// Entity holding the registry singleton component.
        /// </summary>
        public Entity RegistryEntity;

        /// <summary>
        /// Entity holding the validation report buffer.
        /// </summary>
        public Entity ReportEntity;

        /// <summary>
        /// Unique index for this participant within the validation system.
        /// </summary>
        public int ParticipantIndex;

        /// <summary>
        /// Version incremented on each registration to detect stale handles.
        /// </summary>
        public uint Version;

        public readonly bool IsValid => RegistryEntity != Entity.Null && ParticipantIndex >= 0;

        public bool Equals(RegistryContinuityParticipantHandle other)
        {
            return RegistryEntity == other.RegistryEntity &&
                   ReportEntity == other.ReportEntity &&
                   ParticipantIndex == other.ParticipantIndex &&
                   Version == other.Version;
        }

        public override bool Equals(object obj) => obj is RegistryContinuityParticipantHandle other && Equals(other);

        public override int GetHashCode()
        {
            return unchecked((int)math.hash(new uint4(
                (uint)RegistryEntity.Index,
                (uint)ReportEntity.Index,
                (uint)ParticipantIndex,
                Version)));
        }
    }

    /// <summary>
    /// Entry in the custom registry participants buffer.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct RegistryContinuityParticipant : IBufferElementData, IComparable<RegistryContinuityParticipant>
    {
        public Entity RegistryEntity;
        public Entity ReportEntity;
        public FixedString64Bytes Label;
        public uint Version;
        public uint LastUpdateTick;
        public byte RequiresSpatialSync;
        public byte SupportsRewind;
        public byte IsActive;
        public RegistryContinuitySnapshot Snapshot;

        public readonly bool RequiresSpatialSyncFlag => RequiresSpatialSync != 0;
        public readonly bool SupportsRewindFlag => SupportsRewind != 0;
        public readonly bool IsActiveFlag => IsActive != 0;

        public int CompareTo(RegistryContinuityParticipant other)
        {
            var indexCompare = RegistryEntity.Index.CompareTo(other.RegistryEntity.Index);
            return indexCompare != 0 ? indexCompare : RegistryEntity.Version.CompareTo(other.RegistryEntity.Version);
        }
    }

    /// <summary>
    /// Validation report entry for a continuity participant.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ContinuityValidationReport : IBufferElementData
    {
        public Entity RegistryEntity;
        public FixedString64Bytes Label;
        public RegistryContinuityStatus Status;
        public RegistryHealthFlags Flags;
        public uint SpatialVersion;
        public uint RegistrySpatialVersion;
        public uint Delta;
        public int Errors;
        public int Warnings;
        public uint ValidationTick;
    }

    /// <summary>
    /// Singleton tracking the custom registry participants.
    /// </summary>
    public struct RegistryContinuityParticipants : IComponentData
    {
        public int ParticipantCount;
        public uint Version;
        public uint LastRegistrationTick;
    }

    /// <summary>
    /// API for registering custom registries with the continuity validation system.
    /// Game projects use this to enroll their registries (e.g., Space4XColonyRegistry) in validation.
    /// </summary>
    public static class RegistryContinuityApi
    {
        /// <summary>
        /// Registers a custom registry for continuity validation.
        /// </summary>
        /// <param name="entityManager">The entity manager.</param>
        /// <param name="registryEntity">The entity holding the registry singleton.</param>
        /// <param name="registration">Configuration for the registration.</param>
        /// <returns>Handle for subsequent updates.</returns>
        public static RegistryContinuityParticipantHandle RegisterCustomRegistry(
            EntityManager entityManager,
            Entity registryEntity,
            in RegistryContinuityRegistration registration)
        {
            if (registryEntity == Entity.Null)
            {
                return default;
            }

            // Find or create the participants singleton
            var participantsEntity = GetOrCreateParticipantsEntity(entityManager);
            var participants = entityManager.GetComponentData<RegistryContinuityParticipants>(participantsEntity);
            var participantsBuffer = entityManager.GetBuffer<RegistryContinuityParticipant>(participantsEntity);

            // Check if already registered
            for (var i = 0; i < participantsBuffer.Length; i++)
            {
                var existing = participantsBuffer[i];
                if (existing.RegistryEntity == registryEntity)
                {
                    // Update existing registration
                    existing.Label = registration.Label;
                    existing.RequiresSpatialSync = registration.RequiresSpatialSync ? (byte)1 : (byte)0;
                    existing.SupportsRewind = registration.SupportsRewind ? (byte)1 : (byte)0;
                    existing.Snapshot = registration.Snapshot;
                    existing.LastUpdateTick = registration.LastUpdateTick;
                    existing.IsActive = 1;
                    existing.Version++;
                    participantsBuffer[i] = existing;

                    participants.Version++;
                    entityManager.SetComponentData(participantsEntity, participants);

                    return new RegistryContinuityParticipantHandle
                    {
                        RegistryEntity = registryEntity,
                        ReportEntity = existing.ReportEntity,
                        ParticipantIndex = i,
                        Version = existing.Version
                    };
                }
            }

            // Create report entity for this participant
            var reportEntity = entityManager.CreateEntity();
            entityManager.AddBuffer<ContinuityValidationReport>(reportEntity);

            // Add new participant
            var newParticipant = new RegistryContinuityParticipant
            {
                RegistryEntity = registryEntity,
                ReportEntity = reportEntity,
                Label = registration.Label,
                RequiresSpatialSync = registration.RequiresSpatialSync ? (byte)1 : (byte)0,
                SupportsRewind = registration.SupportsRewind ? (byte)1 : (byte)0,
                Snapshot = registration.Snapshot,
                LastUpdateTick = registration.LastUpdateTick,
                IsActive = 1,
                Version = 1
            };

            participantsBuffer.Add(newParticipant);
            var index = participantsBuffer.Length - 1;

            participants.ParticipantCount = participantsBuffer.Length;
            participants.Version++;
            participants.LastRegistrationTick = registration.LastUpdateTick;
            entityManager.SetComponentData(participantsEntity, participants);

            return new RegistryContinuityParticipantHandle
            {
                RegistryEntity = registryEntity,
                ReportEntity = reportEntity,
                ParticipantIndex = index,
                Version = newParticipant.Version
            };
        }

        /// <summary>
        /// Reports an update to a registered custom registry.
        /// </summary>
        /// <param name="entityManager">The entity manager.</param>
        /// <param name="handle">Handle from registration.</param>
        /// <param name="resolvedCount">Number of entries resolved via spatial queries.</param>
        /// <param name="fallbackCount">Number of entries using fallback resolution.</param>
        /// <param name="unmappedCount">Number of entries that couldn't be mapped.</param>
        /// <param name="spatialVersion">Current spatial grid version.</param>
        /// <param name="currentTick">Current tick.</param>
        public static void ReportUpdate(
            EntityManager entityManager,
            in RegistryContinuityParticipantHandle handle,
            int resolvedCount,
            int fallbackCount,
            int unmappedCount,
            uint spatialVersion = 0,
            uint currentTick = 0)
        {
            if (!handle.IsValid)
            {
                return;
            }

            var participantsEntity = GetParticipantsEntity(entityManager);
            if (participantsEntity == Entity.Null)
            {
                return;
            }

            var participantsBuffer = entityManager.GetBuffer<RegistryContinuityParticipant>(participantsEntity);
            if (handle.ParticipantIndex >= participantsBuffer.Length)
            {
                return;
            }

            var participant = participantsBuffer[handle.ParticipantIndex];
            if (participant.RegistryEntity != handle.RegistryEntity || participant.Version != handle.Version)
            {
                return; // Stale handle
            }

            participant.Snapshot = RegistryContinuitySnapshot.WithSpatialData(
                spatialVersion,
                resolvedCount,
                fallbackCount,
                unmappedCount,
                participant.RequiresSpatialSyncFlag);
            participant.LastUpdateTick = currentTick;
            participantsBuffer[handle.ParticipantIndex] = participant;
        }

        /// <summary>
        /// Unregisters a custom registry from continuity validation.
        /// </summary>
        /// <param name="entityManager">The entity manager.</param>
        /// <param name="handle">Handle from registration.</param>
        public static void UnregisterCustomRegistry(
            EntityManager entityManager,
            in RegistryContinuityParticipantHandle handle)
        {
            if (!handle.IsValid)
            {
                return;
            }

            var participantsEntity = GetParticipantsEntity(entityManager);
            if (participantsEntity == Entity.Null)
            {
                return;
            }

            var participants = entityManager.GetComponentData<RegistryContinuityParticipants>(participantsEntity);
            var participantsBuffer = entityManager.GetBuffer<RegistryContinuityParticipant>(participantsEntity);

            if (handle.ParticipantIndex >= participantsBuffer.Length)
            {
                return;
            }

            var participant = participantsBuffer[handle.ParticipantIndex];
            if (participant.RegistryEntity != handle.RegistryEntity)
            {
                return;
            }

            // Mark as inactive rather than removing to preserve indices
            participant.IsActive = 0;
            participantsBuffer[handle.ParticipantIndex] = participant;

            // Clean up report entity
            if (handle.ReportEntity != Entity.Null && entityManager.Exists(handle.ReportEntity))
            {
                entityManager.DestroyEntity(handle.ReportEntity);
            }

            // Update count
            var activeCount = 0;
            for (var i = 0; i < participantsBuffer.Length; i++)
            {
                if (participantsBuffer[i].IsActiveFlag)
                {
                    activeCount++;
                }
            }

            participants.ParticipantCount = activeCount;
            participants.Version++;
            entityManager.SetComponentData(participantsEntity, participants);
        }

        /// <summary>
        /// Gets the validation report buffer for a registered participant.
        /// </summary>
        /// <param name="entityManager">The entity manager.</param>
        /// <param name="handle">Handle from registration.</param>
        /// <param name="report">Output validation report buffer.</param>
        /// <returns>True if the report was found.</returns>
        public static bool TryGetValidationReport(
            EntityManager entityManager,
            in RegistryContinuityParticipantHandle handle,
            out DynamicBuffer<ContinuityValidationReport> report)
        {
            report = default;

            if (!handle.IsValid || handle.ReportEntity == Entity.Null)
            {
                return false;
            }

            if (!entityManager.Exists(handle.ReportEntity))
            {
                return false;
            }

            if (!entityManager.HasBuffer<ContinuityValidationReport>(handle.ReportEntity))
            {
                return false;
            }

            report = entityManager.GetBuffer<ContinuityValidationReport>(handle.ReportEntity);
            return true;
        }

        private static Entity GetOrCreateParticipantsEntity(EntityManager entityManager)
        {
            var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RegistryContinuityParticipants>());
            if (!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingletonEntity();
            }

            var entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, new RegistryContinuityParticipants
            {
                ParticipantCount = 0,
                Version = 0,
                LastRegistrationTick = 0
            });
            entityManager.AddBuffer<RegistryContinuityParticipant>(entity);

            return entity;
        }

        private static Entity GetParticipantsEntity(EntityManager entityManager)
        {
            var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RegistryContinuityParticipants>());
            return query.IsEmptyIgnoreFilter ? Entity.Null : query.GetSingletonEntity();
        }
    }
}

