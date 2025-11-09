from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Duplicates an existing VFX context in a graph"
)
async def duplicate_context(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    context_id: Annotated[int, "ID of the context to duplicate"],
    position_x: Annotated[float | None, "X position for the duplicated context (default: offset from original)"] = None,
    position_y: Annotated[float | None, "Y position for the duplicated context (default: offset from original)"] = None,
) -> dict[str, Any]:
    await ctx.info(f"Duplicating context {context_id} in graph {graph_path}")
    
    params = {
        "graph_path": graph_path,
        "context_id": context_id,
    }
    if position_x is not None:
        params["position_x"] = position_x
    if position_y is not None:
        params["position_y"] = position_y
    
    response = send_command_with_retry("duplicate_context", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

