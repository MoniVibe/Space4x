using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Launch
{
    /// <summary>
    /// Identifies which TRI game this world/session belongs to.
    /// Used by shared launch/menu logic and world-generation stubs.
    /// </summary>
    public enum TriGameId : byte
    {
        Unknown = 0,
        Godgame = 1,
        Space4X = 2,
    }

    public enum LaunchScreen : byte
    {
        None = 0,
        MainMenu = 1,
        NewGame = 2,
        CustomGame = 3,
        Settings = 4,
        Loading = 5,
        InGame = 6,
    }

    public enum LaunchCommandType : byte
    {
        None = 0,
        NavigateTo = 1,
        Back = 2,
        StartNewGame = 3,
        ApplySettings = 4,
        Quit = 5,
        TogglePause = 6,
        Pause = 7,
        Resume = 8,
        StepTicks = 9,
        SpeedNormal = 10,
        SlowMo = 11,
        FastForward = 12,
        SetSpeed = 13,
        RewindToggle = 14,
        StartRewind = 15,
        StopRewind = 16,
        ScrubToTick = 17,
    }

    public enum DifficultyPreset : byte
    {
        Story = 0,
        Easy = 1,
        Normal = 2,
        Hard = 3,
        Brutal = 4,
        Custom = 5,
    }

    public enum DensityPreset : byte
    {
        Sparse = 0,
        Normal = 1,
        Dense = 2,
        Custom = 3,
    }

    public enum WorldGenSizePreset : byte
    {
        Tiny = 0,
        Small = 1,
        Medium = 2,
        Large = 3,
        Huge = 4,
        Custom = 5,
    }

    /// <summary>
    /// Tag indicating a world contains a launch/menu root entity.
    /// Systems should require this tag so they stay inert in smoke/headless scenes unless wired in.
    /// </summary>
    public struct LaunchRootTag : IComponentData
    {
    }

    /// <summary>
    /// Singleton-ish identity for the current game. Authored per project.
    /// </summary>
    public struct TriGame : IComponentData
    {
        public TriGameId Id;
    }

    /// <summary>
    /// Current screen and navigation state for the launch/menu flow.
    /// </summary>
    public struct LaunchMenuState : IComponentData
    {
        public LaunchScreen Screen;
        public LaunchScreen PreviousScreen;
        public byte HasPrevious;
    }

    /// <summary>
    /// One-shot commands emitted by UI code (Mono/UI Toolkit/etc) and consumed by ECS.
    /// Prefer writing draft state directly to components, then enqueue a command to apply.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct LaunchCommand : IBufferElementData
    {
        public LaunchCommandType Type;
        public LaunchScreen TargetScreen;
        public uint Data0;
        public float Data1;
    }

    /// <summary>
    /// Player-facing settings (audio/UX) stub. This is not simulation data.
    /// Game-side bridges can persist/apply these values (PlayerPrefs, files, etc).
    /// </summary>
    public struct UserSettings : IComponentData
    {
        public float MasterVolume01;
        public float MusicVolume01;
        public float SfxVolume01;
        public float UiScale01;
        public byte ColorblindMode;

        public static UserSettings Defaults => new()
        {
            MasterVolume01 = 1f,
            MusicVolume01 = 0.8f,
            SfxVolume01 = 0.9f,
            UiScale01 = 1f,
            ColorblindMode = 0
        };
    }

    /// <summary>
    /// Draft new game setup edited in the menu before being applied.
    /// </summary>
    public struct NewGameDraft : IComponentData
    {
        public uint Seed; // 0 => runtime chooses
        public DifficultyPreset DifficultyPreset;
        public float CustomDifficulty01; // used when preset == Custom
        public DensityPreset DensityPreset;
        public float CustomDensity01; // used when preset == Custom
        public WorldGenSizePreset WorldSizePreset;
        public float CustomWorldSize01; // normalized size, used when preset == Custom
    }

    public static class NewGameDefaults
    {
        public static NewGameDraft DefaultDraft => new()
        {
            Seed = 0,
            DifficultyPreset = DifficultyPreset.Normal,
            CustomDifficulty01 = 0.5f,
            DensityPreset = DensityPreset.Normal,
            CustomDensity01 = 0.5f,
            WorldSizePreset = WorldGenSizePreset.Medium,
            CustomWorldSize01 = 0.5f
        };

        public static float ResolveDifficulty01(in NewGameDraft draft)
        {
            return math.saturate(draft.DifficultyPreset switch
            {
                DifficultyPreset.Story => 0.25f,
                DifficultyPreset.Easy => 0.4f,
                DifficultyPreset.Normal => 0.5f,
                DifficultyPreset.Hard => 0.7f,
                DifficultyPreset.Brutal => 0.85f,
                DifficultyPreset.Custom => draft.CustomDifficulty01,
                _ => 0.5f
            });
        }

        public static float ResolveDensity01(in NewGameDraft draft)
        {
            return math.saturate(draft.DensityPreset switch
            {
                DensityPreset.Sparse => 0.35f,
                DensityPreset.Normal => 0.5f,
                DensityPreset.Dense => 0.7f,
                DensityPreset.Custom => draft.CustomDensity01,
                _ => 0.5f
            });
        }

        public static float ResolveWorldSize01(in NewGameDraft draft)
        {
            return math.saturate(draft.WorldSizePreset switch
            {
                WorldGenSizePreset.Tiny => 0.2f,
                WorldGenSizePreset.Small => 0.35f,
                WorldGenSizePreset.Medium => 0.5f,
                WorldGenSizePreset.Large => 0.75f,
                WorldGenSizePreset.Huge => 0.95f,
                WorldGenSizePreset.Custom => draft.CustomWorldSize01,
                _ => 0.5f
            });
        }
    }

    /// <summary>
    /// Request to generate a new world/session from the current draft settings.
    /// Intended as the bridge point between menu flow and game-specific world generation.
    /// </summary>
    public struct WorldGenRequest : IComponentData
    {
        public TriGameId GameId;
        public uint Seed;
        public float Difficulty01;
        public float Density01;
        public float WorldSize01;
    }

    public enum WorldGenPhase : byte
    {
        Idle = 0,
        Requested = 1,
        Generating = 2,
        Completed = 3,
        Failed = 4,
    }

    /// <summary>
    /// Tracks world generation progress. Stubs may jump Requested -> Completed immediately.
    /// </summary>
    public struct WorldGenStatus : IComponentData
    {
        public WorldGenPhase Phase;
        public float Progress01;
        public FixedString128Bytes StatusText;
    }

    /// <summary>
    /// Optional tag: enables the shared stub worldgen system for early iteration.
    /// Real worldgen implementations should omit this and consume WorldGenRequest directly.
    /// </summary>
    public struct UseWorldGenStubTag : IComponentData
    {
    }

    /// <summary>
    /// One-shot request to quit from the menu. Game-side code decides how to handle it.
    /// </summary>
    public struct QuitRequest : IComponentData
    {
    }

    /// <summary>
    /// Tag indicating an entity can accept launch requests.
    /// </summary>
    public struct LauncherTag : IComponentData
    {
    }

    /// <summary>
    /// Launch queue entry state.
    /// </summary>
    public enum LaunchEntryState : byte
    {
        Pending = 0,
        Launched = 1,
        Consumed = 2
    }

    /// <summary>
    /// Request to launch a payload entity from a launcher.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct LaunchRequest : IBufferElementData
    {
        public Entity SourceEntity;
        public Entity PayloadEntity;
        public uint LaunchTick;
        public float3 InitialVelocity;
        public byte Flags;
    }

    /// <summary>
    /// Internal queued launch entry on a launcher.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct LaunchQueueEntry : IBufferElementData
    {
        public Entity PayloadEntity;
        public uint ScheduledTick;
        public float3 InitialVelocity;
        public LaunchEntryState State;
    }

    /// <summary>
    /// Static config for launchers.
    /// </summary>
    public struct LauncherConfig : IComponentData
    {
        public byte MaxQueueSize;
        public uint CooldownTicks;
        public float DefaultSpeed;

        public static LauncherConfig CreateDefault()
        {
            return new LauncherConfig
            {
                MaxQueueSize = 8,
                CooldownTicks = 10u,
                DefaultSpeed = 10f
            };
        }
    }

    /// <summary>
    /// Runtime state for launchers.
    /// </summary>
    public struct LauncherState : IComponentData
    {
        public uint LastLaunchTick;
        public byte QueueCount;
        public uint Version;
    }

    /// <summary>
    /// Tag written to launched payloads for post-processing.
    /// </summary>
    public struct LaunchedProjectileTag : IComponentData
    {
        public uint LaunchTick;
        public Entity SourceLauncher;
    }
}
