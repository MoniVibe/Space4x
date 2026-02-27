using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space4X.UI
{
    [Serializable]
    public sealed class Space4XUserSettingsData
    {
        public int schema_version = 1;
        public int quality_level = -1;
        public int fullscreen_mode = (int)FullScreenMode.FullScreenWindow;
        public int resolution_width;
        public int resolution_height;
        public float hud_scale = 1f;
        public float font_scale = 1f;
        public int hud_fade_on_power_deficit;
        public float audio_master = 1f;
        public float audio_music = 0.85f;
        public float audio_sfx = 0.9f;
        public int key_toggle_hud = (int)Key.Backquote;
        public int key_toggle_inventory = (int)Key.I;
        public int key_toggle_minimap = (int)Key.M;
        public int key_toggle_notifications = (int)Key.N;
        public int key_toggle_forces_holo = (int)Key.F7;
        public int key_toggle_layout_edit = (int)Key.F8;
        public int key_ui_scale_increase = (int)Key.RightBracket;
        public int key_ui_scale_decrease = (int)Key.LeftBracket;
        public int key_layout_reset = (int)Key.F9;

        public Space4XUserSettingsData Clone()
        {
            return (Space4XUserSettingsData)MemberwiseClone();
        }
    }

    public static class Space4XUserSettingsStore
    {
        public const string PrefsKey = "space4x.ui.settings.v1";
        public const float FontScaleMin = 0.75f;
        public const float FontScaleMax = 1.6f;

        public static Space4XUserSettingsData LoadOrDefault()
        {
            if (!PlayerPrefs.HasKey(PrefsKey))
            {
                return CreateDefaults();
            }

            try
            {
                var json = PlayerPrefs.GetString(PrefsKey, string.Empty);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return CreateDefaults();
                }

                var data = JsonUtility.FromJson<Space4XUserSettingsData>(json);
                return Normalize(data);
            }
            catch
            {
                return CreateDefaults();
            }
        }

        public static Space4XUserSettingsData CreateDefaults()
        {
            var defaults = new Space4XUserSettingsData
            {
                quality_level = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, Mathf.Max(0, QualitySettings.names.Length - 1)),
                fullscreen_mode = (int)Screen.fullScreenMode,
                resolution_width = Screen.currentResolution.width,
                resolution_height = Screen.currentResolution.height,
                hud_scale = 1f,
                font_scale = 1f,
                hud_fade_on_power_deficit = 0,
                audio_master = Mathf.Clamp01(AudioListener.volume),
                audio_music = 0.85f,
                audio_sfx = 0.9f
            };

            return Normalize(defaults);
        }

        public static void Save(Space4XUserSettingsData data)
        {
            var normalized = Normalize(data);
            var json = JsonUtility.ToJson(normalized);
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            if (PlayerPrefs.HasKey(PrefsKey))
            {
                PlayerPrefs.DeleteKey(PrefsKey);
                PlayerPrefs.Save();
            }
        }

        public static Space4XUserSettingsData Normalize(Space4XUserSettingsData data)
        {
            var normalized = data ?? new Space4XUserSettingsData();
            normalized.schema_version = 1;
            var qualityCount = Mathf.Max(1, QualitySettings.names.Length);
            normalized.quality_level = Mathf.Clamp(normalized.quality_level, 0, qualityCount - 1);
            normalized.fullscreen_mode = (int)ResolveFullScreenMode(normalized.fullscreen_mode, Screen.fullScreenMode);
            normalized.resolution_width = Mathf.Max(0, normalized.resolution_width);
            normalized.resolution_height = Mathf.Max(0, normalized.resolution_height);
            normalized.hud_scale = Mathf.Clamp(normalized.hud_scale <= 0f ? 1f : normalized.hud_scale, 0.7f, 1.8f);
            normalized.font_scale = Mathf.Clamp(normalized.font_scale <= 0f ? 1f : normalized.font_scale, FontScaleMin, FontScaleMax);
            normalized.hud_fade_on_power_deficit = normalized.hud_fade_on_power_deficit != 0 ? 1 : 0;
            normalized.audio_master = Mathf.Clamp01(normalized.audio_master);
            normalized.audio_music = Mathf.Clamp01(normalized.audio_music);
            normalized.audio_sfx = Mathf.Clamp01(normalized.audio_sfx);
            normalized.key_toggle_hud = (int)ResolveKey(normalized.key_toggle_hud, Key.Backquote);
            normalized.key_toggle_inventory = (int)ResolveKey(normalized.key_toggle_inventory, Key.I);
            normalized.key_toggle_minimap = (int)ResolveKey(normalized.key_toggle_minimap, Key.M);
            normalized.key_toggle_notifications = (int)ResolveKey(normalized.key_toggle_notifications, Key.N);
            normalized.key_toggle_forces_holo = (int)ResolveKey(normalized.key_toggle_forces_holo, Key.F7);
            normalized.key_toggle_layout_edit = (int)ResolveKey(normalized.key_toggle_layout_edit, Key.F8);
            normalized.key_ui_scale_increase = (int)ResolveKey(normalized.key_ui_scale_increase, Key.RightBracket);
            normalized.key_ui_scale_decrease = (int)ResolveKey(normalized.key_ui_scale_decrease, Key.LeftBracket);
            normalized.key_layout_reset = (int)ResolveKey(normalized.key_layout_reset, Key.F9);
            return normalized;
        }

        public static void ApplyVideoSettings(Space4XUserSettingsData data)
        {
            if (data == null)
                return;

            var qualityCount = QualitySettings.names.Length;
            if (qualityCount > 0)
            {
                var qualityIndex = Mathf.Clamp(data.quality_level, 0, qualityCount - 1);
                if (QualitySettings.GetQualityLevel() != qualityIndex)
                {
                    QualitySettings.SetQualityLevel(qualityIndex, applyExpensiveChanges: true);
                }
            }

            var mode = ResolveFullScreenMode(data.fullscreen_mode, Screen.fullScreenMode);
            if (data.resolution_width > 0 && data.resolution_height > 0)
            {
                Screen.SetResolution(data.resolution_width, data.resolution_height, mode);
            }
            else
            {
                Screen.fullScreenMode = mode;
            }
        }

        public static void ApplyAudioSettings(Space4XUserSettingsData data)
        {
            if (data == null)
                return;

            AudioListener.volume = Mathf.Clamp01(data.audio_master);
        }

        public static FullScreenMode ResolveFullScreenMode(int serializedMode, FullScreenMode fallback)
        {
            if (!Enum.IsDefined(typeof(FullScreenMode), serializedMode))
                return fallback;

            return (FullScreenMode)serializedMode;
        }

        public static Key ResolveKey(int serializedKey, Key fallback)
        {
            if (!Enum.IsDefined(typeof(Key), serializedKey))
                return fallback;

            return (Key)serializedKey;
        }

        public static string FormatKeyLabel(Key key)
        {
            return key switch
            {
                Key.Backquote => "`",
                Key.LeftBracket => "[",
                Key.RightBracket => "]",
                Key.None => "Unbound",
                _ => key.ToString()
            };
        }
    }
}
