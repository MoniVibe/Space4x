from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Automatically arranges nodes in a grid layout for better readability"
)
async def auto_layout_nodes(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    spacing_x: Annotated[float | None, "Horizontal spacing between nodes (default: 200)"] = None,
    spacing_y: Annotated[float | None, "Vertical spacing between nodes (default: 150)"] = None,
    start_x: Annotated[float | None, "Starting X position (default: 0)"] = None,
    start_y: Annotated[float | None, "Starting Y position (default: 0)"] = None,
) -> dict[str, Any]:
    await ctx.info(f"Auto-laying out nodes in graph {graph_path}")
    
    params = {
        "graph_path": graph_path,
    }
    if spacing_x is not None:
        params["spacing_x"] = spacing_x
    if spacing_y is not None:
        params["spacing_y"] = spacing_y
    if start_x is not None:
        params["start_x"] = start_x
    if start_y is not None:
        params["start_y"] = start_y
    
    response = send_command_with_retry("auto_layout_nodes", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

