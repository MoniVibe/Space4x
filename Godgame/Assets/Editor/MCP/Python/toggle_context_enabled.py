from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Enables or disables a VFX context in a graph"
)
async def toggle_context_enabled(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    context_id: Annotated[int, "ID of the context to toggle"],
    enabled: Annotated[bool, "Whether the context should be enabled (true) or disabled (false)"],
) -> dict[str, Any]:
    await ctx.info(f"{'Enabling' if enabled else 'Disabling'} context {context_id} in graph {graph_path}")
    
    params = {
        "graph_path": graph_path,
        "context_id": context_id,
        "enabled": enabled,
    }
    
    response = send_command_with_retry("toggle_context_enabled", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

