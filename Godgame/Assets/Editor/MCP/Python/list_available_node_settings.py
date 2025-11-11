from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Lists all available settings/properties that can be configured on a VFX graph node"
)
async def list_available_node_settings(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    node_id: Annotated[int, "ID of the node to inspect"],
) -> dict[str, Any]:
    await ctx.info(f"Listing available settings for node {node_id} in graph {graph_path}")
    
    params = {
        "graph_path": graph_path,
        "node_id": node_id,
    }
    
    response = send_command_with_retry("list_available_node_settings", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

