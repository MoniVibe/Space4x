using System;

namespace Space4X.UI
{
    public enum Space4XUiUxKernelCommand : byte
    {
        OpenMainMenu = 0,
        OpenShipSelect = 1,
        StartRun = 2,
        HideMenu = 3,
        OpenSettings = 4,
        CloseSettings = 5,
        ApplySettings = 6,
        ResetSettingsToDefaults = 7
    }

    public enum Space4XInRunHudKernelCommand : byte
    {
        ToggleMinimap = 0,
        ToggleNotifications = 1,
        TogglePause = 2,
        SetTimeHalf = 3,
        SetTimeNormal = 4,
        SetTimeFast = 5,
        ToggleInventory = 6,
        ToggleForcesHolo = 7,
        ToggleProduction = 8,
        ToggleShipControl = 9
    }

    public static class Space4XUiUxElementIds
    {
        public const string Root = "space4x.ui.root";
        public const string RootContainer = "space4x.ui.container";
        public const string MainMenuPanel = "space4x.ui.panel.main_menu";
        public const string ShipSelectPanel = "space4x.ui.panel.ship_select";
        public const string SettingsButton = "space4x.ui.button.settings";
        public const string SettingsModal = "space4x.ui.panel.settings";
        public const string SettingsApplyButton = "space4x.ui.button.settings_apply";
        public const string SettingsCloseButton = "space4x.ui.button.settings_close";
        public const string SettingsQualityDropdown = "space4x.ui.dropdown.quality";
        public const string SettingsFullscreenDropdown = "space4x.ui.dropdown.fullscreen";
        public const string SettingsResolutionDropdown = "space4x.ui.dropdown.resolution";
        public const string SettingsHudScaleSlider = "space4x.ui.slider.hud_scale";
        public const string SettingsFontScaleSlider = "space4x.ui.slider.font_scale";
        public const string SettingsHudDeficitFadeToggle = "space4x.ui.toggle.hud_deficit_fade";
        public const string SettingsAudioMasterSlider = "space4x.ui.slider.audio_master";
        public const string SettingsAudioMusicSlider = "space4x.ui.slider.audio_music";
        public const string SettingsAudioSfxSlider = "space4x.ui.slider.audio_sfx";
        public const string NewGameButton = "space4x.ui.button.new_game";
        public const string StartRunButton = "space4x.ui.button.start_run";
        public const string BackButton = "space4x.ui.button.back";
        public const string PrevShipButton = "space4x.ui.button.ship_prev";
        public const string NextShipButton = "space4x.ui.button.ship_next";
        public const string ShipNameLabel = "space4x.ui.label.ship_name";
        public const string StatusLabel = "space4x.ui.label.status";
        public const string DifficultySlider = "space4x.ui.slider.difficulty";
        public const string DifficultyValueLabel = "space4x.ui.label.difficulty";
    }

    public static class Space4XInRunHudElementIds
    {
        public const string Root = "space4x.ui.hud.root";
        public const string RootPanel = "space4x.ui.hud.panel.root";
        public const string StatColumn = "space4x.ui.hud.column.stats";
        public const string HealthLabel = "space4x.ui.hud.label.health";
        public const string ShieldsLabel = "space4x.ui.hud.label.shields";
        public const string ArmorLabel = "space4x.ui.hud.label.armor";
        public const string SuppliesLabel = "space4x.ui.hud.label.supplies";
        public const string FuelLabel = "space4x.ui.hud.label.fuel";
        public const string FuelReserveLabel = "space4x.ui.hud.label.fuel_reserve";
        public const string FoodLabel = "space4x.ui.hud.label.food";
        public const string CrewLabel = "space4x.ui.hud.label.crew";
        public const string AmmoLabel = "space4x.ui.hud.label.ammo";
        public const string PowerLabel = "space4x.ui.hud.label.power";
        public const string TimeLabel = "space4x.ui.hud.label.time";
        public const string SpeedWidgetPanel = "space4x.ui.hud.panel.speed_widget";
        public const string SpeedWidgetSpeedLabel = "space4x.ui.hud.label.speed";
        public const string SpeedWidgetDrawLabel = "space4x.ui.hud.label.engine_draw";
        public const string ShipControlPanel = "space4x.ui.hud.panel.ship_control";
        public const string ShipControlSummaryLabel = "space4x.ui.hud.label.ship_control_summary";
        public const string ShipControlStatusList = "space4x.ui.hud.list.ship_control_status";
        public const string ShipControlThermalList = "space4x.ui.hud.list.ship_control_thermal";
        public const string PowerRoutingPanel = "space4x.ui.hud.panel.power_routing";
        public const string ForcesHoloPanel = "space4x.ui.hud.panel.forces_holo";
        public const string ForcesHeadingLabel = "space4x.ui.hud.label.heading";
        public const string ForcesVelocityLabel = "space4x.ui.hud.label.velocity";
        public const string ForcesPushLabel = "space4x.ui.hud.label.push";
        public const string MinimapPanel = "space4x.ui.hud.panel.minimap";
        public const string MinimapViewport = "space4x.ui.hud.minimap.viewport";
        public const string MinimapSummaryLabel = "space4x.ui.hud.label.minimap_summary";
        public const string MinimapOverlayModeLabel = "space4x.ui.hud.label.minimap_overlay_mode";
        public const string MinimapContactList = "space4x.ui.hud.list.minimap_contacts";
        public const string TargetPanel = "space4x.ui.hud.panel.target";
        public const string TargetPrimaryLabel = "space4x.ui.hud.label.target_primary";
        public const string TargetSecondaryLabel = "space4x.ui.hud.label.target_secondary";
        public const string TargetHullLabel = "space4x.ui.hud.label.target_hull";
        public const string InventoryPanel = "space4x.ui.hud.panel.inventory";
        public const string InventoryList = "space4x.ui.hud.list.inventory";
        public const string InventoryHintLabel = "space4x.ui.hud.label.inventory_hint";
        public const string InventoryShipLabel = "space4x.ui.hud.label.inventory_ship";
        public const string InventorySegmentsColumn = "space4x.ui.hud.column.inventory_segments";
        public const string InventoryCargoColumn = "space4x.ui.hud.column.inventory_cargo";
        public const string InventoryPopupPrefix = "space4x.ui.hud.inventory_popup";
        public const string ProductionPanel = "space4x.ui.hud.panel.production";
        public const string ProductionList = "space4x.ui.hud.list.production";
        public const string ProductionQueueList = "space4x.ui.hud.list.production_queue";
        public const string NotificationPanel = "space4x.ui.hud.panel.notifications";
        public const string NotificationList = "space4x.ui.hud.list.notifications";
        public const string UtilityControlsPanel = "space4x.ui.hud.panel.utility_controls";
        public const string TimeControlsPanel = "space4x.ui.hud.panel.time_controls";
        public const string ToggleMinimapButton = "space4x.ui.hud.button.toggle_minimap";
        public const string CycleMinimapOverlayButton = "space4x.ui.hud.button.cycle_minimap_overlay";
        public const string ToggleInventoryButton = "space4x.ui.hud.button.toggle_inventory";
        public const string ToggleProductionButton = "space4x.ui.hud.button.toggle_production";
        public const string ToggleShipControlButton = "space4x.ui.hud.button.toggle_ship_control";
        public const string ToggleNotificationsButton = "space4x.ui.hud.button.toggle_notifications";
        public const string TogglePauseButton = "space4x.ui.hud.button.toggle_pause";
        public const string TimeHalfButton = "space4x.ui.hud.button.time_half";
        public const string TimeNormalButton = "space4x.ui.hud.button.time_normal";
        public const string TimeFastButton = "space4x.ui.hud.button.time_fast";
    }

    [Serializable]
    public struct Space4XUiUxKernelSnapshot
    {
        public string timestamp_utc;
        public int frame;
        public string scene_name;
        public string state;
        public int menu_visible;
        public int run_active;
        public int main_menu_panel_visible;
        public int ship_select_panel_visible;
        public string status_text;
        public int selected_ship_index;
        public string selected_ship_id;
        public string selected_ship_name;
        public int difficulty;
        public int difficulty_min;
        public int difficulty_max;
        public string action_map;
        public string focused_element_name;
        public string focused_element_type;
        public int new_game_enabled;
        public int start_run_enabled;
        public int back_enabled;
        public int settings_visible;
        public int settings_dirty;
        public int settings_rebinding;
        public string settings_rebind_target;
        public int settings_quality_index;
        public string settings_quality_name;
        public string settings_fullscreen_mode;
        public string settings_resolution;
        public float settings_hud_scale;
        public float settings_font_scale;
        public int settings_hud_deficit_fade_enabled;
        public float settings_audio_master;
        public float settings_audio_music;
        public float settings_audio_sfx;
        public string settings_toggle_hud_key;
        public string settings_toggle_inventory_key;
        public string settings_toggle_minimap_key;
        public string settings_toggle_notifications_key;
        public string settings_toggle_forces_holo_key;
        public string settings_toggle_layout_edit_key;
        public string settings_ui_scale_increase_key;
        public string settings_ui_scale_decrease_key;
        public string settings_layout_reset_key;
    }

    [Serializable]
    public sealed class Space4XUiUxElementSnapshot
    {
        public string id = string.Empty;
        public string hierarchy_path = string.Empty;
        public string element_type = string.Empty;
        public string text = string.Empty;
        public string class_list = string.Empty;
        public int displayed;
        public int visible;
        public int enabled;
        public int focusable;
        public int focused;
        public float x;
        public float y;
        public float width;
        public float height;
    }

    [Serializable]
    public sealed class Space4XMinimapContactKernelSnapshot
    {
        public string entity_ref = string.Empty;
        public string callsign = string.Empty;
        public string relation = string.Empty;
        public int relation_score;
        public string relation_stance = string.Empty;
        public string relation_source = string.Empty;
        public string color_token = string.Empty;
        public float confidence;
        public float threat_level;
        public float distance;
        public float bearing_deg;
        public float relative_x;
        public float relative_z;
        public float speed;
        public float closing_speed;
        public float em_signature;
        public float gravitic_signature;
        public float thermal_signature;
        public float psi_signature;
    }

    [Serializable]
    public sealed class Space4XInventoryLimbKernelSnapshot
    {
        public string limb_id = string.Empty;
        public string family = string.Empty;
        public float integrity;
        public float exposure;
    }

    [Serializable]
    public sealed class Space4XInventoryModuleKernelSnapshot
    {
        public string module_ref = string.Empty;
        public int segment_index;
        public int slot_index;
        public string slot_size = string.Empty;
        public string slot_state = string.Empty;
        public string module_id = string.Empty;
        public string module_class = string.Empty;
        public string mount = string.Empty;
        public string size = string.Empty;
        public string manufacturer_id = string.Empty;
        public float health_current;
        public float health_max;
        public float health_ratio;
        public float quality;
        public int tier;
        public float power_draw_mw;
        public float mass_tons;
        public float thermal_current;
        public float thermal_capacity;
        public float thermal_ratio;
        public string thermal_source = string.Empty;
        public int thermal_saturated;
        public string limb_profile_summary = string.Empty;
        public int organs_count;
        public Space4XInventoryLimbKernelSnapshot[] organs = Array.Empty<Space4XInventoryLimbKernelSnapshot>();
    }

    [Serializable]
    public sealed class Space4XShipStatusEffectKernelSnapshot
    {
        public string effect_type = string.Empty;
        public string category = string.Empty;
        public string behavior = string.Empty;
        public float duration_seconds;
        public float value;
        public int stacks;
        public int max_stacks;
        public float severity_01;
        public int is_buff;
        public string source_entity_ref = string.Empty;
    }

    [Serializable]
    public sealed class Space4XShipModuleThermalKernelSnapshot
    {
        public string module_ref = string.Empty;
        public string module_label = string.Empty;
        public float thermal_current;
        public float thermal_capacity;
        public float thermal_ratio;
        public string thermal_source = string.Empty;
        public int thermal_saturated;
    }

    [Serializable]
    public sealed class Space4XInventorySegmentKernelSnapshot
    {
        public string segment_id = string.Empty;
        public string segment_label = string.Empty;
        public int module_count;
        public Space4XInventoryModuleKernelSnapshot[] modules = Array.Empty<Space4XInventoryModuleKernelSnapshot>();
    }

    [Serializable]
    public sealed class Space4XInventoryCatalogModuleKernelSnapshot
    {
        public string module_id = string.Empty;
        public string module_class = string.Empty;
        public string mount = string.Empty;
        public string size = string.Empty;
        public string manufacturer_id = string.Empty;
        public float quality;
        public int tier;
        public float power_draw_mw;
        public float mass_tons;
        public string function = string.Empty;
        public string function_description = string.Empty;
    }

    [Serializable]
    public sealed class Space4XProductionQueueKernelSnapshot
    {
        public string entry_id = string.Empty;
        public string recipe_id = string.Empty;
        public string state = string.Empty;
        public string source = string.Empty;
        public int priority;
        public int batch_count;
        public float eta_seconds;
        public float required_power_mw;
        public float required_crew;
    }

    [Serializable]
    public sealed class Space4XProductionFacilityKernelSnapshot
    {
        public string entity_ref = string.Empty;
        public string facility_kind = string.Empty;
        public string business_type = string.Empty;
        public float capacity;
        public float throughput;
        public int queue_capacity;
        public int queue_count;
        public int active_queue_index;
        public float active_eta_seconds;
        public int blocked;
        public float required_power_mw;
        public float assigned_power_mw;
        public float required_crew;
        public float assigned_crew;
        public float seat_fill_01;
        public float skill_factor_01;
        public float effective_throughput;
        public int shift_index;
        public float power_scale;
        public string selected_recipe_id = string.Empty;
        public string selected_limb_id = string.Empty;
        public string[] available_recipes = Array.Empty<string>();
        public string[] available_limbs = Array.Empty<string>();
        public Space4XProductionQueueKernelSnapshot[] queue = Array.Empty<Space4XProductionQueueKernelSnapshot>();
    }

    [Serializable]
    public struct Space4XInRunHudKernelSnapshot
    {
        public string timestamp_utc;
        public int frame;
        public string scene_name;
        public int hud_visible;
        public int hud_deficit_fade_enabled;
        public float hud_deficit_fade_alpha;
        public int run_active;
        public int flagship_bound;
        public float health_current;
        public float health_max;
        public float health_ratio;
        public float shields_current;
        public float shields_max;
        public float shields_ratio;
        public float armor_rating;
        public float supplies_current;
        public float supplies_max;
        public float supplies_ratio;
        public float fuel_current;
        public float fuel_max;
        public float fuel_ratio;
        public float food_current;
        public float food_max;
        public float food_ratio;
        public float crew_current;
        public float crew_max;
        public float crew_ratio;
        public float ammo_current;
        public float ammo_max;
        public float ammo_ratio;
        public float power_available_mw;
        public float power_capacity_mw;
        public float power_draw_mw;
        public float power_ratio;
        public float power_deficit_mw;
        public string power_deficit_tags;
        public float speed_current_ups;
        public float engine_power_draw_mw;
        public int engine_power_estimated;
        public float engine_fuel_draw_per_tick;
        public int engine_fuel_estimated;
        public int ship_control_visible;
        public string ship_control_summary;
        public int ship_control_status_count;
        public int ship_control_thermal_count;
        public Space4XShipStatusEffectKernelSnapshot[] ship_control_statuses;
        public Space4XShipModuleThermalKernelSnapshot[] ship_control_modules;
        public int power_routing_profile;
        public float power_route_engines_pct;
        public float power_route_weapons_pct;
        public float power_route_shields_pct;
        public float power_route_reactor_pct;
        public float power_route_sensors_pct;
        public float power_route_total_pct;
        public float power_route_engines_factor;
        public float power_route_weapons_factor;
        public float power_route_shields_factor;
        public float power_route_reactor_factor;
        public float power_route_sensors_factor;
        public float power_route_reactor_signature_factor;
        public float power_route_jam_risk_per_tick;
        public float power_route_overclock_pct;
        public float power_route_underclock_pct;
        public int forces_holo_visible;
        public float heading_deg;
        public float velocity_forward_ups;
        public float velocity_lateral_ups;
        public float velocity_vertical_ups;
        public float accel_forward_ups2;
        public float accel_lateral_ups2;
        public float accel_vertical_ups2;
        public float accel_total_ups2;
        public int accel_estimated;
        public int minimap_visible;
        public int minimap_widget_3d;
        public int minimap_camera_ready;
        public int minimap_contacts_tracked;
        public int minimap_self_count;
        public int minimap_ally_count;
        public int minimap_neutral_count;
        public int minimap_hostile_count;
        public int minimap_unknown_count;
        public int minimap_contacts_window_count;
        public string minimap_relation_counts;
        public string minimap_overlay_mode;
        public int minimap_overlay_visible_count;
        public Space4XMinimapContactKernelSnapshot[] minimap_contacts;
        public int target_selected;
        public int target_multi_count;
        public string target_entity_ref;
        public string target_callsign;
        public string target_relation;
        public int target_relation_score;
        public string target_relation_stance;
        public string target_relation_source;
        public string target_color_token;
        public float target_distance;
        public float target_bearing_deg;
        public float target_speed;
        public float target_closing_speed;
        public float target_hull_current;
        public float target_hull_max;
        public float target_hull_ratio;
        public int inventory_visible;
        public int inventory_line_count;
        public string inventory_summary;
        public string inventory_ship_preset_id;
        public string inventory_ship_label;
        public string inventory_ship_archetype;
        public int inventory_segment_count;
        public int inventory_module_count;
        public int inventory_popup_count;
        public string inventory_highlight_module_ref;
        public Space4XInventorySegmentKernelSnapshot[] inventory_segments;
        public string[] inventory_cargo_lines;
        public int inventory_available_module_count;
        public Space4XInventoryCatalogModuleKernelSnapshot[] inventory_available_modules;
        public int production_visible;
        public string production_summary;
        public int production_facility_count;
        public int production_selected_index;
        public string production_selected_entity_ref;
        public string production_selected_recipe_id;
        public string production_selected_limb_id;
        public int production_selected_shift;
        public float production_selected_power_scale;
        public int production_selected_queue_count;
        public Space4XProductionFacilityKernelSnapshot[] production_facilities;
        public int notifications_visible;
        public int notifications_count;
        public int notifications_window_count;
        public string notifications_latest;
        public string notifications_window;
        public uint tick;
        public float world_seconds;
        public int time_paused;
        public float time_speed_multiplier;
        public string action_map;
        public string focused_element_name;
        public string focused_element_type;
        public int layout_edit_mode;
        public float ui_global_scale;
        public float status_panel_x;
        public float status_panel_y;
        public float status_panel_w;
        public float status_panel_h;
        public float status_panel_scale;
        public float minimap_panel_x;
        public float minimap_panel_y;
        public float minimap_panel_w;
        public float minimap_panel_h;
        public float minimap_panel_scale;
        public float target_panel_x;
        public float target_panel_y;
        public float target_panel_w;
        public float target_panel_h;
        public float target_panel_scale;
        public float inventory_panel_x;
        public float inventory_panel_y;
        public float inventory_panel_w;
        public float inventory_panel_h;
        public float inventory_panel_scale;
        public float notification_panel_x;
        public float notification_panel_y;
        public float notification_panel_w;
        public float notification_panel_h;
        public float notification_panel_scale;
        public float speed_panel_x;
        public float speed_panel_y;
        public float speed_panel_w;
        public float speed_panel_h;
        public float speed_panel_scale;
        public float ship_control_panel_x;
        public float ship_control_panel_y;
        public float ship_control_panel_w;
        public float ship_control_panel_h;
        public float ship_control_panel_scale;
        public float forces_panel_x;
        public float forces_panel_y;
        public float forces_panel_w;
        public float forces_panel_h;
        public float forces_panel_scale;
        public float power_routing_panel_x;
        public float power_routing_panel_y;
        public float power_routing_panel_w;
        public float power_routing_panel_h;
        public float power_routing_panel_scale;
        public float production_panel_x;
        public float production_panel_y;
        public float production_panel_w;
        public float production_panel_h;
        public float production_panel_scale;
        public float hud_panel_x;
        public float hud_panel_y;
    }
}
