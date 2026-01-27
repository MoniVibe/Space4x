using PureDOTS.Runtime.Config;

namespace PureDOTS.Runtime.History
{
    public static class HistoryConfigVars
    {
        [RuntimeConfigVar("history.physics.enabled", "0", Flags = RuntimeConfigFlags.Save, Description = "Enable physics world history capture (costly).")]
        public static RuntimeConfigVar PhysicsHistoryEnabled;

        [RuntimeConfigVar("history.physics.length", "32", Flags = RuntimeConfigFlags.Save, Description = "Number of frames stored in the physics history ring buffer.")]
        public static RuntimeConfigVar PhysicsHistoryLength;
    }
}


