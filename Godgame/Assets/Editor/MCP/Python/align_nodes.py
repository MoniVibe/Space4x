from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Aligns multiple nodes horizontally or vertically with specified spacing"
)
async def align_nodes(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    node_ids: Annotated[list[int], "Array of node IDs to align"],
    alignment: Annotated[str | None, "Alignment direction: 'horizontal' or 'vertical' (default: 'horizontal')"] = None,
    spacing: Annotated[float | None, "Spacing between aligned nodes (default: 200)"] = None,
) -> dict[str, Any]:
    await ctx.info(f"Aligning {len(node_ids)} nodes in graph {graph_path}")
    
    params = {
        "graph_path": graph_path,
        "node_ids": node_ids,
    }
    if alignment is not None:
        params["alignment"] = alignment
    if spacing is not None:
        params["spacing"] = spacing
    
    response = send_command_with_retry("align_nodes", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

