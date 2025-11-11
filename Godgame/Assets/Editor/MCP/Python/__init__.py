# PureDOTS Custom MCP Tools Registry
# This module imports all custom tools so they're registered with the MCP server

# Instance Management
from . import identify_unity_instance
from . import list_unity_instances
from . import switch_unity_instance

# Component Operations
from . import add_component_to_gameobject
from . import configure_component_property
from . import get_component_info

# Project Info
from . import get_project_info

# Graph Systems
from . import get_graph_structure
from . import add_node_to_graph
from . import connect_graph_nodes
from . import disconnect_graph_nodes
from . import list_graph_variants
from . import remove_node_from_graph
from . import move_graph_node
from . import set_graph_node_property
from . import connect_flow_contexts
from . import disconnect_flow_contexts
from . import list_context_blocks
from . import add_block_to_context
from . import remove_block_from_context
from . import move_block_in_context
from . import describe_node_ports
from . import describe_node_settings
from . import set_slot_value
from . import list_exposed_parameters
from . import create_exposed_parameter
# Graph Lifecycle
from . import list_graphs
from . import describe_vfx_graph
from . import create_vfx_graph
from . import duplicate_graph
from . import set_visual_effect_graph
from . import list_graph_instances
from . import collect_vfx_dataset
from . import capture_vfx_preview_window
from . import capture_vfx_multi_angle
from . import capture_vfx_headless
# Context & Node Management
from . import create_context
from . import duplicate_context
from . import toggle_context_enabled
from . import duplicate_node
from . import toggle_block_enabled
# Connection & Metadata
from . import list_data_connections
from . import get_slot_metadata
from . import list_available_node_settings
# Parameter Management
from . import rename_exposed_parameter
from . import delete_exposed_parameter
from . import set_exposed_parameter_property
from . import bind_parameter_to_object
from . import unbind_parameter
# Scene & Runtime Integration
from . import spawn_visual_effect_prefab
from . import swap_visual_effect_graph
from . import play_effect
from . import stop_effect
from . import send_vfx_event
from . import set_vfx_parameter
from . import render_vfx_preview
from . import create_vfx_instance
# Diagnostics & Layout
from . import get_vfx_graph_diagnostics
from . import export_graph_snapshot
from . import auto_layout_nodes
from . import align_nodes
# Batch/VFX Builders
from . import build_vfx_graph_tree

# Prefab & Scene Operations
from . import create_prefab
from . import instantiate_prefab
from . import create_scene_with_setup
from . import save_scene_as

# Script & Asset Authoring
from . import create_scriptable_object_instance
from . import create_monobehaviour_script
from . import find_assets_by_type

# Core Asset Creation
from . import create_material
from . import create_texture
from . import create_mesh

# Transform Utilities
from . import set_transform
from . import get_transform

# Timeline Operations
from . import create_timeline_asset
from . import add_timeline_track
from . import add_timeline_clip
from . import set_timeline_playhead

# Audio Operations
from . import play_audio_clip
from . import set_audio_source_property

# Project Automation
from . import enter_play_mode
from . import exit_play_mode
from . import refresh_assets
from . import list_scripting_defines
from . import add_scripting_define
from . import remove_scripting_define

# Shader Graph Lifecycle
from . import create_shader_graph
from . import duplicate_shader_graph
from . import get_shader_graph_structure

# Shader Graph Node & Property Operations
from . import add_node_to_shader_graph
from . import connect_shader_graph_nodes
from . import add_shader_graph_property

# Shader Graph Diagnostics
from . import get_shader_graph_diagnostics

__all__ = [
    # Instance Management
    "identify_unity_instance",
    "list_unity_instances",
    "switch_unity_instance",
    # Component Operations
    "add_component_to_gameobject",
    "configure_component_property",
    "get_component_info",
    # Project Info
    "get_project_info",
    # Graph Systems
    "get_graph_structure",
    "add_node_to_graph",
    "connect_graph_nodes",
    "disconnect_graph_nodes",
    "list_graph_variants",
    "remove_node_from_graph",
    "move_graph_node",
    "set_graph_node_property",
    "build_vfx_graph_tree",
    # Flow & Block Authoring
    "connect_flow_contexts",
    "disconnect_flow_contexts",
    "list_context_blocks",
    "add_block_to_context",
    "remove_block_from_context",
    "move_block_in_context",
    # Discovery & Configuration
    "describe_node_ports",
    "describe_node_settings",
    "set_slot_value",
    "list_exposed_parameters",
    "create_exposed_parameter",
    # Graph Lifecycle
    "list_graphs",
    "describe_vfx_graph",
    "create_vfx_graph",
    "collect_vfx_dataset",
    "capture_vfx_preview_window",
    "capture_vfx_multi_angle",
    "capture_vfx_headless",
    "duplicate_graph",
    "set_visual_effect_graph",
    "list_graph_instances",
    # Context & Node Management
    "create_context",
    "duplicate_context",
    "toggle_context_enabled",
    "duplicate_node",
    "toggle_block_enabled",
    # Connection & Metadata
    "list_data_connections",
    "get_slot_metadata",
    "list_available_node_settings",
    # Parameter Management
    "rename_exposed_parameter",
    "delete_exposed_parameter",
    "set_exposed_parameter_property",
    "bind_parameter_to_object",
    "unbind_parameter",
    # Scene & Runtime Integration
    "spawn_visual_effect_prefab",
    "swap_visual_effect_graph",
    "play_effect",
    "stop_effect",
    "send_vfx_event",
    "set_vfx_parameter",
    "render_vfx_preview",
    "create_vfx_instance",
    # Diagnostics & Layout
    "get_vfx_graph_diagnostics",
    "export_graph_snapshot",
    "auto_layout_nodes",
    "align_nodes",
    # Prefab & Scene Operations
    "create_prefab",
    "instantiate_prefab",
    "create_scene_with_setup",
    "save_scene_as",
    # Script & Asset Authoring
    "create_scriptable_object_instance",
    "create_monobehaviour_script",
    "find_assets_by_type",
    # Core Asset Creation
    "create_material",
    "create_texture",
    "create_mesh",
    # Transform Utilities
    "set_transform",
    "get_transform",
    # Timeline Operations
    "create_timeline_asset",
    "add_timeline_track",
    "add_timeline_clip",
    "set_timeline_playhead",
    # Audio Operations
    "play_audio_clip",
    "set_audio_source_property",
    # Project Automation
    "enter_play_mode",
    "exit_play_mode",
    "refresh_assets",
    "list_scripting_defines",
    "add_scripting_define",
    "remove_scripting_define",
    # Shader Graph Lifecycle
    "create_shader_graph",
    "duplicate_shader_graph",
    "get_shader_graph_structure",
    # Shader Graph Node & Property Operations
    "add_node_to_shader_graph",
    "connect_shader_graph_nodes",
    "add_shader_graph_property",
    # Shader Graph Diagnostics
    "get_shader_graph_diagnostics",
]

