from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Remove a node from a Unity graph"
)
async def remove_node_from_graph(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    node_id: Annotated[str | int, "Node ID to remove"],
) -> dict[str, Any]:
    await ctx.info(f"Removing node {node_id} from graph {graph_path}")
    
    params = {
        "graph_path": graph_path,
        "node_id": node_id,
    }
    
    response = send_command_with_retry("remove_node_from_graph", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

