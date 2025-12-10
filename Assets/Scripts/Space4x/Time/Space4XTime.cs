using UnityEngine;

namespace Space4X
{
    /// <summary>
    /// Space4X time facade. Currently forwards to UnityEngine.Time.
    /// </summary>
    public static class Time
    {
        public static float deltaTime => UnityEngine.Time.deltaTime;
        public static float time => UnityEngine.Time.time;
        public static int frameCount => UnityEngine.Time.frameCount;
    }
}


