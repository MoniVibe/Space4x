# MCP Tools for Unity Projects

Shared MCP (Model Context Protocol) tools that enable AI assistants to interact with Unity projects.

## Quick Start

**One-click setup:**
1. Copy `PureDOTS/Assets/Editor/MCP/` to `<YourProject>/Assets/Editor/MCP/`
2. In Unity, run: `Tools > MCP > Run One-Click MCP Setup`
3. Rebuild MCP server: `Window > MCP For Unity > Rebuild Server`

That's it! Your tools are now available.

## Current Tools

**Instance Management:**
- `identify_unity_instance` - Identify which Unity project/instance is active
- `list_unity_instances` - List all running Unity instances
- `switch_unity_instance` - Switch active Unity instance

**Project Info:**
- `get_project_info` - Get project metadata (name, Unity version, packages, etc.)

**Graph Builders:**
- `build_vfx_graph_tree` - Create a starter VFX graph with spawn/initialize/update/output contexts in one call

**Components:**
- `get_component_info` - Get component properties and metadata
- `add_component_to_gameobject` - Add a component to a GameObject
- `configure_component_property` - Set component property values

## Structure

- `Handlers/` - C# tool handlers (7 handlers)
- `Helpers/` - Shared utilities (reflection helpers, response formatting, attribute bridge)
- `Python/` - Python tool definitions (synced to MCP server)
- `Setup/` - One-click setup utility

## Adding New Tools

1. Create Python file in `Python/` with `@mcp_for_unity_tool` decorator
2. Create C# handler in `Handlers/` with `[McpForUnityTool("tool_name")]` attribute
3. Re-run one-click setup to register the new Python file

The setup automatically discovers all Python files and registers them with the MCP server.
