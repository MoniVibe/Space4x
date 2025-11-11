from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Gets compilation errors, warnings, and statistics for a VFX graph"
)
async def get_vfx_graph_diagnostics(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
) -> dict[str, Any]:
    await ctx.info(f"Getting diagnostics for graph {graph_path}")
    
    params = {
        "graph_path": graph_path,
    }
    
    response = send_command_with_retry("get_vfx_graph_diagnostics", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

