using PureDOTS.Input;
using Unity.Entities;
#if UNITY_EDITOR
using UnityEngine;
#endif

namespace PureDOTS.Systems
{
    /// <summary>
    /// Processes save/load command events and calls into game-specific save/load pipeline.
    /// Games should implement their own serialization logic.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct SaveLoadSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RtsInputSingletonTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Entity rtsInputEntity = SystemAPI.GetSingletonEntity<RtsInputSingletonTag>();

            if (!state.EntityManager.HasBuffer<SaveLoadCommandEvent>(rtsInputEntity))
            {
                return;
            }

            var saveLoadBuffer = state.EntityManager.GetBuffer<SaveLoadCommandEvent>(rtsInputEntity);

            for (int i = 0; i < saveLoadBuffer.Length; i++)
            {
                var command = saveLoadBuffer[i];
                ProcessSaveLoadCommand(ref state, command);
            }

            saveLoadBuffer.Clear();
        }

        private void ProcessSaveLoadCommand(ref SystemState state, SaveLoadCommandEvent command)
        {
            const string quickSaveSlot = "quick.sav";
#if UNITY_EDITOR
            UnityEngine.Debug.Log($"[SaveLoadSystem] Received {command.Kind} targeting slot '{quickSaveSlot}'.");
#endif

            switch (command.Kind)
            {
                case SaveLoadCommandKind.QuickSave:
                    // TODO: Call into game-specific save pipeline
                    // Example: SaveSystem.Save(quickSaveSlot);
                    break;

                case SaveLoadCommandKind.QuickLoad:
                    // TODO: Call into game-specific load pipeline
                    // Example: SaveSystem.Load(quickSaveSlot);
                    break;
            }
        }
    }
}






















