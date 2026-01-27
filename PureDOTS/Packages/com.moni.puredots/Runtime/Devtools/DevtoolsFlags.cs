namespace PureDOTS.Devtools
{
    /// <summary>
    /// Devtools feature flags and compile-time guards.
    /// Tools are gated by DEVTOOLS_ENABLED compile symbol.
    /// </summary>
    public static class DevtoolsFlags
    {
#if DEVTOOLS_ENABLED
        public const bool Enabled = true;
#else
        public const bool Enabled = false;
#endif
    }
}























