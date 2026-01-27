using System.Threading;

namespace PureDOTS.Rendering
{
    public static class RenderCatalogVersionUtility
    {
        private static int _versionCounter = 1;

        public static uint Next()
        {
            var next = Interlocked.Increment(ref _versionCounter);
            return next == 0 ? 1u : (uint)next;
        }
    }
}
