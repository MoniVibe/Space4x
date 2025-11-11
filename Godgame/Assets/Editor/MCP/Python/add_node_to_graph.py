from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Add a node to a Unity graph"
)
async def add_node_to_graph(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    node_type: Annotated[str, "Type of node to add (e.g., 'Multiply', 'Add', 'Sample Texture 2D')"],
    position_x: Annotated[float, "X position for the node"],
    position_y: Annotated[float, "Y position for the node"],
    properties: Annotated[dict[str, Any] | None, "Optional node properties dictionary"] = None,
) -> dict[str, Any]:
    await ctx.info(f"Adding node {node_type} to graph {graph_path} at ({position_x}, {position_y})")
    
    params = {
        "graph_path": graph_path,
        "node_type": node_type,
        "position_x": position_x,
        "position_y": position_y,
    }
    if properties is not None:
        params["properties"] = properties
    
    response = send_command_with_retry("add_node_to_graph", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

