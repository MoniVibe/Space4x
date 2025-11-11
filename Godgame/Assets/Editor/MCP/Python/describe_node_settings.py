from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Get detailed information about all settings/properties on a node"
)
async def describe_node_settings(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    node_id: Annotated[str | int, "Node instance ID"],
) -> dict[str, Any]:
    await ctx.info(f"Describing settings for node {node_id} in graph {graph_path}")
    
    params = {
        "graph_path": graph_path,
        "node_id": node_id,
    }
    
    response = send_command_with_retry("describe_node_settings", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

