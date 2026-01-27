using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Registry
{
    /// <summary>
    /// Helper for running continuity validation in edit-mode and playmode tests.
    /// </summary>
    public static class ContinuityValidationRunner
    {
        /// <summary>
        /// Result of a validation run.
        /// </summary>
        public struct ValidationResult
        {
            public int TotalParticipants;
            public int ActiveParticipants;
            public int Errors;
            public int Warnings;
            public bool HasFailures;
            public FixedString512Bytes Summary;

            public readonly bool IsValid => !HasFailures && Errors == 0;
        }

        /// <summary>
        /// Validates a single registered participant.
        /// </summary>
        /// <param name="world">The world containing the participant.</param>
        /// <param name="handle">Handle to the participant.</param>
        /// <param name="currentSpatialVersion">Current spatial grid version.</param>
        /// <returns>Validation result.</returns>
        public static ValidationResult Validate(
            World world,
            in RegistryContinuityParticipantHandle handle,
            uint currentSpatialVersion = 0)
        {
            var result = new ValidationResult
            {
                TotalParticipants = 1,
                ActiveParticipants = 0,
                Errors = 0,
                Warnings = 0,
                HasFailures = false,
                Summary = default
            };

            if (!handle.IsValid)
            {
                result.Errors = 1;
                result.HasFailures = true;
                result.Summary = new FixedString512Bytes("Invalid participant handle");
                return result;
            }

            var entityManager = world.EntityManager;
            var participantsEntity = GetParticipantsEntity(entityManager);
            if (participantsEntity == Entity.Null)
            {
                result.Errors = 1;
                result.HasFailures = true;
                result.Summary = new FixedString512Bytes("No participants singleton found");
                return result;
            }

            var participantsBuffer = entityManager.GetBuffer<RegistryContinuityParticipant>(participantsEntity);
            if (handle.ParticipantIndex >= participantsBuffer.Length)
            {
                result.Errors = 1;
                result.HasFailures = true;
                result.Summary = new FixedString512Bytes("Participant index out of range");
                return result;
            }

            var participant = participantsBuffer[handle.ParticipantIndex];
            if (!participant.IsActiveFlag)
            {
                result.Summary = new FixedString512Bytes("Participant is inactive");
                return result;
            }

            result.ActiveParticipants = 1;

            // Validate spatial continuity if required
            if (participant.RequiresSpatialSyncFlag)
            {
                if (!participant.Snapshot.HasSpatialData)
                {
                    result.Errors++;
                    result.HasFailures = true;
                    AppendToSummary(ref result.Summary, "Missing spatial data");
                }
                else if (currentSpatialVersion > 0)
                {
                    var delta = currentSpatialVersion >= participant.Snapshot.SpatialVersion
                        ? currentSpatialVersion - participant.Snapshot.SpatialVersion
                        : participant.Snapshot.SpatialVersion - currentSpatialVersion;

                    if (delta > 10) // Critical threshold
                    {
                        result.Errors++;
                        result.HasFailures = true;
                        AppendToSummary(ref result.Summary, $"Spatial version drift: {delta}");
                    }
                    else if (delta > 1) // Warning threshold
                    {
                        result.Warnings++;
                        AppendToSummary(ref result.Summary, $"Spatial version warning: {delta}");
                    }
                }

                // Check unmapped count
                if (participant.Snapshot.SpatialUnmappedCount > 0)
                {
                    result.Warnings++;
                    AppendToSummary(ref result.Summary, $"Unmapped entries: {participant.Snapshot.SpatialUnmappedCount}");
                }
            }

            if (result.Summary.Length == 0)
            {
                result.Summary = new FixedString512Bytes("OK");
            }

            return result;
        }

        /// <summary>
        /// Validates all registered participants.
        /// </summary>
        /// <param name="world">The world containing the participants.</param>
        /// <param name="currentSpatialVersion">Current spatial grid version.</param>
        /// <returns>Aggregate validation result.</returns>
        public static ValidationResult ValidateAll(
            World world,
            uint currentSpatialVersion = 0)
        {
            var result = new ValidationResult
            {
                TotalParticipants = 0,
                ActiveParticipants = 0,
                Errors = 0,
                Warnings = 0,
                HasFailures = false,
                Summary = default
            };

            var entityManager = world.EntityManager;
            var participantsEntity = GetParticipantsEntity(entityManager);
            if (participantsEntity == Entity.Null)
            {
                result.Summary = new FixedString512Bytes("No participants registered");
                return result;
            }

            var participantsBuffer = entityManager.GetBuffer<RegistryContinuityParticipant>(participantsEntity);
            result.TotalParticipants = participantsBuffer.Length;

            for (var i = 0; i < participantsBuffer.Length; i++)
            {
                var participant = participantsBuffer[i];
                if (!participant.IsActiveFlag)
                {
                    continue;
                }

                result.ActiveParticipants++;

                // Validate spatial continuity if required
                if (participant.RequiresSpatialSyncFlag)
                {
                    if (!participant.Snapshot.HasSpatialData)
                    {
                        result.Errors++;
                        result.HasFailures = true;
                        AppendToSummary(ref result.Summary, $"{participant.Label}: Missing spatial data");
                    }
                    else if (currentSpatialVersion > 0)
                    {
                        var delta = currentSpatialVersion >= participant.Snapshot.SpatialVersion
                            ? currentSpatialVersion - participant.Snapshot.SpatialVersion
                            : participant.Snapshot.SpatialVersion - currentSpatialVersion;

                        if (delta > 10)
                        {
                            result.Errors++;
                            result.HasFailures = true;
                            AppendToSummary(ref result.Summary, $"{participant.Label}: Spatial drift {delta}");
                        }
                        else if (delta > 1)
                        {
                            result.Warnings++;
                        }
                    }

                    if (participant.Snapshot.SpatialUnmappedCount > 0)
                    {
                        result.Warnings++;
                    }
                }
            }

            if (result.Summary.Length == 0)
            {
                result.Summary = result.ActiveParticipants > 0
                    ? new FixedString512Bytes($"OK: {result.ActiveParticipants} participants validated")
                    : new FixedString512Bytes("No active participants");
            }

            return result;
        }

        /// <summary>
        /// Gets the validation reports for all registered participants.
        /// </summary>
        /// <param name="world">The world containing the participants.</param>
        /// <param name="allocator">Allocator for the result list.</param>
        /// <returns>List of validation reports.</returns>
        public static NativeList<ContinuityValidationReport> GetAllReports(
            World world,
            Allocator allocator)
        {
            var reports = new NativeList<ContinuityValidationReport>(allocator);

            var entityManager = world.EntityManager;
            var participantsEntity = GetParticipantsEntity(entityManager);
            if (participantsEntity == Entity.Null)
            {
                return reports;
            }

            var participantsBuffer = entityManager.GetBuffer<RegistryContinuityParticipant>(participantsEntity);
            for (var i = 0; i < participantsBuffer.Length; i++)
            {
                var participant = participantsBuffer[i];
                if (!participant.IsActiveFlag || participant.ReportEntity == Entity.Null)
                {
                    continue;
                }

                if (!entityManager.Exists(participant.ReportEntity))
                {
                    continue;
                }

                if (!entityManager.HasBuffer<ContinuityValidationReport>(participant.ReportEntity))
                {
                    continue;
                }

                var reportBuffer = entityManager.GetBuffer<ContinuityValidationReport>(participant.ReportEntity);
                for (var j = 0; j < reportBuffer.Length; j++)
                {
                    reports.Add(reportBuffer[j]);
                }
            }

            return reports;
        }

        private static Entity GetParticipantsEntity(EntityManager entityManager)
        {
            var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RegistryContinuityParticipants>());
            return query.IsEmptyIgnoreFilter ? Entity.Null : query.GetSingletonEntity();
        }

        private static void AppendToSummary(ref FixedString512Bytes summary, string message)
        {
            if (summary.Length > 0)
            {
                summary.Append("; ");
            }
            summary.Append(message);
        }
    }
}

