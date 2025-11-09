from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Lists all data connections in a VFX graph, optionally filtered by node ID"
)
async def list_data_connections(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    node_id: Annotated[int | None, "Optional node ID to filter connections (only show connections involving this node)"] = None,
) -> dict[str, Any]:
    await ctx.info(f"Listing data connections in graph {graph_path}")
    
    params = {
        "graph_path": graph_path,
    }
    if node_id is not None:
        params["node_id"] = node_id
    
    response = send_command_with_retry("list_data_connections", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

