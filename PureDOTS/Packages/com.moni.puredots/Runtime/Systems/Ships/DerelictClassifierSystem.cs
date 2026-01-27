using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Ships;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Ships
{
    /// <summary>
    /// Classifies ships as derelict based on hull/crew/power state.
    /// Runs after damage systems.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(ModuleCriticalEffectsSystem))]
    public partial struct DerelictClassifierSystem : ISystem
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
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var job = new DerelictClassifierJob();
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct DerelictClassifierJob : IJobEntity
        {
            void Execute(
                Entity entity,
                ref DerelictState derelict,
                in HullState hull,
                in CrewState crew)
            {
                byte currentStage = derelict.Stage;

                // Stage 0 = active, 1 = disabled, 2 = derelict, 3 = wreck

                // Check if hull is destroyed
                if (hull.HP <= 0f)
                {
                    // Hull destroyed: move to derelict or wreck
                    if (currentStage < 2)
                    {
                        derelict.Stage = 2; // Derelict
                    }
                    else if (crew.Alive == 0 && crew.InPods == 0)
                    {
                        derelict.Stage = 3; // Wreck (no survivors)
                    }
                    return;
                }

                // Check if ship is disabled (no power + no command + crew 0)
                // TODO: Check power and command systems
                // For now, use crew count as proxy
                if (crew.Alive == 0 && crew.InPods == 0)
                {
                    if (currentStage < 1)
                    {
                        derelict.Stage = 1; // Disabled
                    }
                    else if (hull.HP < hull.MaxHP * 0.5f)
                    {
                        derelict.Stage = 2; // Derelict (damaged and crewless)
                    }
                    return;
                }

                // Ship is still active
                if (currentStage > 0 && hull.HP > hull.MaxHP * 0.8f && crew.Alive > 0)
                {
                    // Recovery: if hull repaired and crew present, return to active
                    derelict.Stage = 0;
                }
            }
        }
    }
}

