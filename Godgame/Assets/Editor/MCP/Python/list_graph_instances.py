from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Lists all VisualEffect components in the active scene and their assigned graphs"
)
async def list_graph_instances(
    ctx: Context,
) -> dict[str, Any]:
    await ctx.info("Listing VFX graph instances in scene")
    
    params = {}
    
    response = send_command_with_retry("list_graph_instances", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

