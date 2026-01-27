using System.Diagnostics;
using PureDOTS.Runtime.Telemetry;
using Unity.Entities;

namespace PureDOTS.Systems
{
    internal static class SystemGroupInstrumentationUtility
    {
        public static void Record(ComponentSystemGroup group, Stopwatch stopwatch, FrameTimingGroup groupId)
        {
            FrameTimingRecorderSystem.RecordGroupTiming(
                group.World,
                groupId,
                (float)stopwatch.Elapsed.TotalMilliseconds,
                0,
                FrameTimingRecorderSystem.IsCatchUp(group.World));
        }
    }

    public partial class TimeSystemGroup
    {
        private Stopwatch _timingStopwatch;

        protected override void OnCreate()
        {
            base.OnCreate();
            _timingStopwatch = new Stopwatch();
        }

        protected override void OnUpdate()
        {
            _timingStopwatch.Restart();
            base.OnUpdate();
            _timingStopwatch.Stop();
            SystemGroupInstrumentationUtility.Record(this, _timingStopwatch, FrameTimingGroup.Time);
        }
    }

    public partial class EnvironmentSystemGroup
    {
        private Stopwatch _timingStopwatch;

        protected override void OnCreate()
        {
            base.OnCreate();
            _timingStopwatch = new Stopwatch();
        }

        protected override void OnUpdate()
        {
            _timingStopwatch.Restart();
            base.OnUpdate();
            _timingStopwatch.Stop();
            SystemGroupInstrumentationUtility.Record(this, _timingStopwatch, FrameTimingGroup.Environment);
        }
    }

    public partial class SpatialSystemGroup
    {
        private Stopwatch _timingStopwatch;

        protected override void OnCreate()
        {
            base.OnCreate();
            _timingStopwatch = new Stopwatch();
        }

        protected override void OnUpdate()
        {
            _timingStopwatch.Restart();
            base.OnUpdate();
            _timingStopwatch.Stop();
            SystemGroupInstrumentationUtility.Record(this, _timingStopwatch, FrameTimingGroup.Spatial);
        }
    }

    public partial class AISystemGroup
    {
        private Stopwatch _timingStopwatch;

        protected override void OnCreate()
        {
            base.OnCreate();
            _timingStopwatch = new Stopwatch();
        }

        protected override void OnUpdate()
        {
            _timingStopwatch.Restart();
            base.OnUpdate();
            _timingStopwatch.Stop();
            SystemGroupInstrumentationUtility.Record(this, _timingStopwatch, FrameTimingGroup.AI);
        }
    }

    public partial class VillagerSystemGroup
    {
        private Stopwatch _timingStopwatch;

        protected override void OnCreate()
        {
            base.OnCreate();
            _timingStopwatch = new Stopwatch();
        }

        protected override void OnUpdate()
        {
            _timingStopwatch.Restart();
            base.OnUpdate();
            _timingStopwatch.Stop();
            SystemGroupInstrumentationUtility.Record(this, _timingStopwatch, FrameTimingGroup.Villager);
        }
    }

    public partial class ResourceSystemGroup
    {
        private Stopwatch _timingStopwatch;

        protected override void OnCreate()
        {
            base.OnCreate();
            _timingStopwatch = new Stopwatch();
        }

        protected override void OnUpdate()
        {
            _timingStopwatch.Restart();
            base.OnUpdate();
            _timingStopwatch.Stop();
            SystemGroupInstrumentationUtility.Record(this, _timingStopwatch, FrameTimingGroup.Resource);
        }
    }

    public partial class MiracleEffectSystemGroup
    {
        private Stopwatch _timingStopwatch;

        protected override void OnCreate()
        {
            base.OnCreate();
            _timingStopwatch = new Stopwatch();
        }

        protected override void OnUpdate()
        {
            _timingStopwatch.Restart();
            base.OnUpdate();
            _timingStopwatch.Stop();
            SystemGroupInstrumentationUtility.Record(this, _timingStopwatch, FrameTimingGroup.Miracle);
        }
    }

    public partial class GameplaySystemGroup
    {
        private Stopwatch _timingStopwatch;

        protected override void OnCreate()
        {
            base.OnCreate();
            _timingStopwatch = new Stopwatch();
        }

        protected override void OnUpdate()
        {
            _timingStopwatch.Restart();
            base.OnUpdate();
            _timingStopwatch.Stop();
            SystemGroupInstrumentationUtility.Record(this, _timingStopwatch, FrameTimingGroup.Gameplay);
        }
    }

    public partial class HandSystemGroup
    {
        private Stopwatch _timingStopwatch;

        protected override void OnCreate()
        {
            base.OnCreate();
            _timingStopwatch = new Stopwatch();
        }

        protected override void OnUpdate()
        {
            _timingStopwatch.Restart();
            base.OnUpdate();
            _timingStopwatch.Stop();
            SystemGroupInstrumentationUtility.Record(this, _timingStopwatch, FrameTimingGroup.Hand);
        }
    }

}
