using PureDOTS.Runtime.Launch;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Launch
{
    /// <summary>
    /// Shared main menu / launch flow state machine.
    /// Inert unless a LaunchRootTag entity exists in the world (wired in by game scenes).
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    public partial struct LaunchMenuSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LaunchRootTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var root = SystemAPI.GetSingletonEntity<LaunchRootTag>();
            var em = state.EntityManager;

            EnsureDefaults(em, root);

            var menuState = em.GetComponentData<LaunchMenuState>(root);
            var draft = em.GetComponentData<NewGameDraft>(root);
            var settings = em.GetComponentData<UserSettings>(root);

            var commands = em.GetBuffer<LaunchCommand>(root);
            if (commands.Length == 0)
            {
                return;
            }

            for (int i = 0; i < commands.Length; i++)
            {
                var cmd = commands[i];
                switch (cmd.Type)
                {
                    case LaunchCommandType.NavigateTo:
                        Navigate(ref menuState, cmd.TargetScreen);
                        break;

                    case LaunchCommandType.Back:
                        if (menuState.HasPrevious != 0)
                        {
                            var current = menuState.Screen;
                            menuState.Screen = menuState.PreviousScreen;
                            menuState.PreviousScreen = current;
                        }
                        break;

                    case LaunchCommandType.ApplySettings:
                        settings.MasterVolume01 = math.saturate(settings.MasterVolume01);
                        settings.MusicVolume01 = math.saturate(settings.MusicVolume01);
                        settings.SfxVolume01 = math.saturate(settings.SfxVolume01);
                        settings.UiScale01 = math.clamp(settings.UiScale01, 0.5f, 2f);
                        break;

                    case LaunchCommandType.StartNewGame:
                        StartNewGame(ref state, em, root, ref menuState, in draft);
                        break;

                    case LaunchCommandType.Quit:
                        if (!em.HasComponent<QuitRequest>(root))
                        {
                            em.AddComponent<QuitRequest>(root);
                        }
                        break;
                }
            }

            commands.Clear();
            em.SetComponentData(root, menuState);
            em.SetComponentData(root, settings);
        }

        private static void EnsureDefaults(EntityManager em, Entity root)
        {
            if (!em.HasComponent<TriGame>(root))
            {
                em.AddComponentData(root, new TriGame { Id = TriGameId.Unknown });
            }

            if (!em.HasComponent<LaunchMenuState>(root))
            {
                em.AddComponentData(root, new LaunchMenuState
                {
                    Screen = LaunchScreen.MainMenu,
                    PreviousScreen = LaunchScreen.None,
                    HasPrevious = 0
                });
            }

            if (!em.HasComponent<UserSettings>(root))
            {
                em.AddComponentData(root, UserSettings.Defaults);
            }

            if (!em.HasComponent<NewGameDraft>(root))
            {
                em.AddComponentData(root, NewGameDefaults.DefaultDraft);
            }

            if (!em.HasComponent<WorldGenStatus>(root))
            {
                em.AddComponentData(root, new WorldGenStatus
                {
                    Phase = WorldGenPhase.Idle,
                    Progress01 = 0f,
                    StatusText = default
                });
            }

            if (!em.HasBuffer<LaunchCommand>(root))
            {
                em.AddBuffer<LaunchCommand>(root);
            }
        }

        private static void Navigate(ref LaunchMenuState menuState, LaunchScreen target)
        {
            if (target == LaunchScreen.None || target == menuState.Screen)
            {
                return;
            }

            menuState.PreviousScreen = menuState.Screen;
            menuState.HasPrevious = 1;
            menuState.Screen = target;
        }

        private static void StartNewGame(ref SystemState state, EntityManager em, Entity root, ref LaunchMenuState menuState, in NewGameDraft draft)
        {
            var gameId = TriGameId.Unknown;
            if (em.HasComponent<TriGame>(root))
            {
                gameId = em.GetComponentData<TriGame>(root).Id;
            }

            var seed = draft.Seed;
            if (seed == 0)
            {
                // Non-deterministic by design (menu-level). Callers can set an explicit seed for determinism.
                var t = (uint)math.clamp(state.WorldUnmanaged.Time.ElapsedTime * 1000.0, 1.0, uint.MaxValue);
                seed = 0xC0FFEEu ^ t;
                if (seed == 0) seed = 1;
            }

            var request = new WorldGenRequest
            {
                GameId = gameId,
                Seed = seed,
                Difficulty01 = NewGameDefaults.ResolveDifficulty01(in draft),
                Density01 = NewGameDefaults.ResolveDensity01(in draft),
                WorldSize01 = NewGameDefaults.ResolveWorldSize01(in draft),
            };

            if (em.HasComponent<WorldGenRequest>(root))
            {
                em.SetComponentData(root, request);
            }
            else
            {
                em.AddComponentData(root, request);
            }

            var status = em.GetComponentData<WorldGenStatus>(root);
            status.Phase = WorldGenPhase.Requested;
            status.Progress01 = 0f;
            status.StatusText = new FixedString128Bytes("WorldGen requested");
            em.SetComponentData(root, status);

            Navigate(ref menuState, LaunchScreen.Loading);
        }
    }

    /// <summary>
    /// Shared world-generation stub that completes immediately.
    /// Only enabled when UseWorldGenStubTag is present on the launch root.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct WorldGenStubSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LaunchRootTag>();
            state.RequireForUpdate<UseWorldGenStubTag>();
            state.RequireForUpdate<WorldGenRequest>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var root = SystemAPI.GetSingletonEntity<LaunchRootTag>();
            var em = state.EntityManager;

            if (!em.HasComponent<WorldGenStatus>(root))
            {
                return;
            }

            var status = em.GetComponentData<WorldGenStatus>(root);
            if (status.Phase != WorldGenPhase.Requested && status.Phase != WorldGenPhase.Generating)
            {
                return;
            }

            status.Phase = WorldGenPhase.Completed;
            status.Progress01 = 1f;
            status.StatusText = new FixedString128Bytes("WorldGen stub complete");
            em.SetComponentData(root, status);

            if (em.HasComponent<WorldGenRequest>(root))
            {
                em.RemoveComponent<WorldGenRequest>(root);
            }

            if (em.HasComponent<LaunchMenuState>(root))
            {
                var menu = em.GetComponentData<LaunchMenuState>(root);
                menu.Screen = LaunchScreen.InGame;
                em.SetComponentData(root, menu);
            }
        }
    }
}
