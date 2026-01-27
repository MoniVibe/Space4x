using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Social;
using PureDOTS.Runtime.Time;

namespace PureDOTS.Runtime.Systems.Social
{
    /// <summary>
    /// System that detects first-time meetings and creates initial relations.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct EntityMeetingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Process interaction requests that may create new relations
            foreach (var (request, entity) in SystemAPI.Query<RefRO<RecordInteractionRequest>>().WithEntityAccess())
            {
                var req = request.ValueRO;
                
                // Process for Entity A
                if (SystemAPI.HasBuffer<EntityRelation>(req.EntityA))
                {
                    var relations = SystemAPI.GetBuffer<EntityRelation>(req.EntityA);
                    int existingIndex = RelationCalculator.FindRelationIndex(relations, req.EntityB);
                    
                    if (existingIndex < 0)
                    {
                        // First meeting - create new relation
                        sbyte initialImpression = RelationCalculator.CalculateInitialImpression(
                            50, // Default charisma
                            50, // Default reputation
                            false, // Assume different factions
                            (uint)(req.EntityA.Index + req.EntityB.Index + currentTick));

                        relations.Add(new EntityRelation
                        {
                            OtherEntity = req.EntityB,
                            Type = RelationType.Stranger,
                            Intensity = initialImpression,
                            InteractionCount = 0,
                            FirstMetTick = currentTick,
                            LastInteractionTick = currentTick,
                            Trust = 50,
                            Familiarity = 0,
                            Respect = 50,
                            Fear = 0
                        });

                        // Emit first meeting event
                        if (SystemAPI.HasBuffer<FirstMeetingEvent>(req.EntityA))
                        {
                            var events = SystemAPI.GetBuffer<FirstMeetingEvent>(req.EntityA);
                            events.Add(new FirstMeetingEvent
                            {
                                OtherEntity = req.EntityB,
                                InitialImpression = initialImpression,
                                Tick = currentTick
                            });
                        }
                    }
                }

                // Process for Entity B if mutual
                if (req.IsMutual && SystemAPI.HasBuffer<EntityRelation>(req.EntityB))
                {
                    var relations = SystemAPI.GetBuffer<EntityRelation>(req.EntityB);
                    int existingIndex = RelationCalculator.FindRelationIndex(relations, req.EntityA);
                    
                    if (existingIndex < 0)
                    {
                        sbyte initialImpression = RelationCalculator.CalculateInitialImpression(
                            50, 50, false,
                            (uint)(req.EntityB.Index + req.EntityA.Index + currentTick));

                        relations.Add(new EntityRelation
                        {
                            OtherEntity = req.EntityA,
                            Type = RelationType.Stranger,
                            Intensity = initialImpression,
                            InteractionCount = 0,
                            FirstMetTick = currentTick,
                            LastInteractionTick = currentTick,
                            Trust = 50,
                            Familiarity = 0,
                            Respect = 50,
                            Fear = 0
                        });

                        if (SystemAPI.HasBuffer<FirstMeetingEvent>(req.EntityB))
                        {
                            var events = SystemAPI.GetBuffer<FirstMeetingEvent>(req.EntityB);
                            events.Add(new FirstMeetingEvent
                            {
                                OtherEntity = req.EntityA,
                                InitialImpression = initialImpression,
                                Tick = currentTick
                            });
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// System that processes interaction requests and updates relations.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EntityMeetingSystem))]
    [BurstCompile]
    public partial struct RelationInteractionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Process interaction requests
            foreach (var (request, entity) in SystemAPI.Query<RefRO<RecordInteractionRequest>>().WithEntityAccess())
            {
                var req = request.ValueRO;

                // Update Entity A's relation with B
                if (SystemAPI.HasBuffer<EntityRelation>(req.EntityA))
                {
                    var relations = SystemAPI.GetBuffer<EntityRelation>(req.EntityA);
                    int index = RelationCalculator.FindRelationIndex(relations, req.EntityB);
                    
                    if (index >= 0)
                    {
                        var relation = relations[index];
                        var oldType = relation.Type;
                        var oldIntensity = relation.Intensity;

                        // Calculate changes
                        sbyte intensityChange = req.IntensityChange != 0 
                            ? req.IntensityChange 
                            : RelationCalculator.CalculateIntensityChange(relation.Type, req.Outcome);
                        
                        byte familiarityGain = RelationCalculator.CalculateFamiliarityGain(
                            req.Outcome, relation.Familiarity, 2);
                        
                        sbyte trustChange = RelationCalculator.CalculateTrustChange(
                            req.Outcome, relation.Trust);

                        // Apply changes
                        relation.Intensity = (sbyte)math.clamp(relation.Intensity + intensityChange, -100, 100);
                        relation.Trust = (byte)math.clamp(relation.Trust + trustChange, 0, 100);
                        relation.Familiarity = (byte)math.min(relation.Familiarity + familiarityGain, 100);
                        relation.InteractionCount++;
                        relation.LastInteractionTick = currentTick;

                        // Update relationship type
                        relation.Type = RelationCalculator.DetermineRelationType(
                            relation.Intensity, relation.InteractionCount, relation.Type);

                        relations[index] = relation;

                        // Emit change event if type changed
                        if (relation.Type != oldType && SystemAPI.HasBuffer<RelationChangedEvent>(req.EntityA))
                        {
                            var events = SystemAPI.GetBuffer<RelationChangedEvent>(req.EntityA);
                            events.Add(new RelationChangedEvent
                            {
                                OtherEntity = req.EntityB,
                                OldType = oldType,
                                NewType = relation.Type,
                                OldIntensity = oldIntensity,
                                NewIntensity = relation.Intensity,
                                Tick = currentTick
                            });
                        }
                    }
                }

                // Update Entity B's relation with A if mutual
                if (req.IsMutual && SystemAPI.HasBuffer<EntityRelation>(req.EntityB))
                {
                    var relations = SystemAPI.GetBuffer<EntityRelation>(req.EntityB);
                    int index = RelationCalculator.FindRelationIndex(relations, req.EntityA);
                    
                    if (index >= 0)
                    {
                        var relation = relations[index];
                        var oldType = relation.Type;
                        var oldIntensity = relation.Intensity;

                        sbyte intensityChange = req.IntensityChange != 0 
                            ? req.IntensityChange 
                            : RelationCalculator.CalculateIntensityChange(relation.Type, req.Outcome);
                        
                        byte familiarityGain = RelationCalculator.CalculateFamiliarityGain(
                            req.Outcome, relation.Familiarity, 2);
                        
                        sbyte trustChange = RelationCalculator.CalculateTrustChange(
                            req.Outcome, relation.Trust);

                        relation.Intensity = (sbyte)math.clamp(relation.Intensity + intensityChange, -100, 100);
                        relation.Trust = (byte)math.clamp(relation.Trust + trustChange, 0, 100);
                        relation.Familiarity = (byte)math.min(relation.Familiarity + familiarityGain, 100);
                        relation.InteractionCount++;
                        relation.LastInteractionTick = currentTick;

                        relation.Type = RelationCalculator.DetermineRelationType(
                            relation.Intensity, relation.InteractionCount, relation.Type);

                        relations[index] = relation;

                        if (relation.Type != oldType && SystemAPI.HasBuffer<RelationChangedEvent>(req.EntityB))
                        {
                            var events = SystemAPI.GetBuffer<RelationChangedEvent>(req.EntityB);
                            events.Add(new RelationChangedEvent
                            {
                                OtherEntity = req.EntityA,
                                OldType = oldType,
                                NewType = relation.Type,
                                OldIntensity = oldIntensity,
                                NewIntensity = relation.Intensity,
                                Tick = currentTick
                            });
                        }
                    }
                }

                // Destroy the request
                ecb.DestroyEntity(entity);
            }
        }
    }
}

