using PureDOTS.Runtime.Launch;
using UnityEngine;

namespace PureDOTS.Authoring.Launch
{
    [CreateAssetMenu(fileName = "UserSettings", menuName = "PureDOTS/Launch/User Settings", order = 21)]
    public sealed class UserSettingsDef : ScriptableObject
    {
        [Range(0f, 1f)] public float masterVolume01 = 1f;
        [Range(0f, 1f)] public float musicVolume01 = 0.8f;
        [Range(0f, 1f)] public float sfxVolume01 = 0.9f;
        [Range(0.5f, 2f)] public float uiScale01 = 1f;

        [Tooltip("0=Off, 1..n reserved.")]
        public byte colorblindMode;

        public UserSettings ToComponent()
        {
            return new UserSettings
            {
                MasterVolume01 = Mathf.Clamp01(masterVolume01),
                MusicVolume01 = Mathf.Clamp01(musicVolume01),
                SfxVolume01 = Mathf.Clamp01(sfxVolume01),
                UiScale01 = Mathf.Clamp(uiScale01, 0.5f, 2f),
                ColorblindMode = colorblindMode
            };
        }
    }
}

