using PureDOTS.Runtime.Launch;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring.Launch
{
    [DisallowMultipleComponent]
    public sealed class LaunchRootAuthoring : MonoBehaviour
    {
        [Header("Project Identity")]
        public TriGameId gameId = TriGameId.Unknown;

        [Header("Menu Start")]
        public LaunchScreen startScreen = LaunchScreen.MainMenu;

        [Header("Defaults")]
        [Tooltip("Optional preset used to seed the NewGameDraft on boot.")]
        public NewGamePresetDef defaultNewGamePreset;

        [Tooltip("Optional settings asset used to seed UserSettings on boot.")]
        public UserSettingsDef defaultUserSettings;

        [Header("Development")]
        [Tooltip("Enable the shared world-gen stub system (Requested -> Completed immediately).")]
        public bool useWorldGenStub = true;

        private sealed class Baker : Baker<LaunchRootAuthoring>
        {
            public override void Bake(LaunchRootAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent<LaunchRootTag>(entity);
                AddComponent(entity, new TriGame { Id = authoring.gameId });
                AddComponent(entity, new LaunchMenuState
                {
                    Screen = authoring.startScreen,
                    PreviousScreen = LaunchScreen.None,
                    HasPrevious = 0
                });

                var settings = authoring.defaultUserSettings != null
                    ? authoring.defaultUserSettings.ToComponent()
                    : UserSettings.Defaults;
                AddComponent(entity, settings);

                var draft = authoring.defaultNewGamePreset != null
                    ? authoring.defaultNewGamePreset.ToDraft()
                    : NewGameDefaults.DefaultDraft;
                AddComponent(entity, draft);

                AddComponent(entity, new WorldGenStatus
                {
                    Phase = WorldGenPhase.Idle,
                    Progress01 = 0f,
                    StatusText = default
                });

                AddBuffer<LaunchCommand>(entity);

                if (authoring.useWorldGenStub)
                {
                    AddComponent<UseWorldGenStubTag>(entity);
                }
            }
        }
    }
}

