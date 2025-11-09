# MCP Custom Tools Debugging Guide

## Current Status

**Python Tools:**
- ✅ 18 Python files created in `Assets/Editor/MCP/Python/`
- ✅ Python files synced: "18 copied" (per console log)
- ❌ Still only 22 tools visible (should be 22 default + 18 custom = 40 total)

**C# Handlers:**
- ✅ 18 C# handlers created with `[McpForUnityTool]` attributes
- ❌ Not being auto-discovered by MCP For Unity
- ❌ Our McpToolDispatcher finds 0 handlers

## Issues

### Issue 1: Python Tools Not Appearing

**Symptoms:**
- Python files synced successfully (18 copied)
- Only 22 tools visible (default MCP tools only)
- No errors in console about Python tools

**Possible Causes:**
1. **Server needs restart**: Python tools are imported when MCP server starts. After sync, server needs to restart to load new tools.
2. **Import errors in Python files**: If Python files have syntax or import errors, FastMCP won't register them.
3. **C# handlers missing**: Python tools call C# handlers. If handlers don't exist when Python tool executes, it might fail silently.

**Debug Steps:**
1. Check sync directory: `MCPForUnity/UnityMcpServer~/src/tools/custom/` - verify Python files are there
2. Check Python file content - ensure `@mcp_for_unity_tool` decorator is present
3. Check server logs for import errors: `~/Library/Application Support/UnityMCP/Logs/unity_mcp_server.log`
4. Restart MCP server (stop and start, or rebuild)

### Issue 2: C# Handlers Not Auto-Discovered

**Symptoms:**
- Console shows "Auto-discovered 10 tools" (default ones only)
- Our 18 handlers not discovered
- Our McpToolDispatcher finds 0 handlers

**Possible Causes:**
1. **Bridge attribute mismatch**: Our bridge `McpForUnityToolAttribute` might not match what MCP For Unity expects
2. **Namespace issue**: Attribute is in `MCPForUnity.Editor.Helpers` but discovery might look elsewhere
3. **Assembly loading order**: Handlers might load after discovery happens

**Debug Steps:**
1. Check if MCP For Unity provides its own attribute (should be in `com.coplaydev.unity-mcp` package)
2. Run: `Tools > Space4X > MCP > Test Handler Discovery` - checks if handlers are discoverable
3. Check if handlers compile correctly (no errors)
4. Verify attribute is applied correctly: `[McpForUnityTool("tool_name")]`

## According to CUSTOM_TOOLS.md

**Python Tools:**
- Auto-discover when:
  - ✅ File added to PythonToolsAsset (done via sync)
  - ✅ File synced to `MCPForUnity/UnityMcpServer~/src/tools/custom/` (18 copied)
  - ✅ File imported during server startup (needs restart?)
  - ✅ Decorator `@mcp_for_unity_tool` used (✓ all files have it)

**C# Handlers:**
- Auto-discover when:
  - ✅ Class has `[McpForUnityTool]` attribute (our bridge)
  - ✅ Class has `public static HandleCommand(JObject)` method
  - ✅ Unity loads the assembly (should happen)
  - ❌ **BUT**: MCP For Unity's auto-discovery isn't finding them

## Next Steps

1. **Verify Python files are in sync directory** ✅ (check via file explorer)
2. **Check Python files for syntax errors** (verify imports work)
3. **Restart MCP server completely** (stop, start, not just rebuild)
4. **Verify C# handlers use correct attribute** (check if MCP For Unity provides its own)
5. **Check server logs** for registration messages

## Expected Behavior

After proper setup:
- **Python tools**: Should appear as 18 MCP tools via `@mcp_for_unity_tool` decorator
- **C# handlers**: Should be auto-discovered by MCP For Unity's CommandRegistry
- **Total**: 22 default + 18 custom = **40 tools**

## Current Reality

- **Python tools**: 18 files synced, but not appearing as tools
- **C# handlers**: Not auto-discovered
- **Total**: Still only 22 tools

