from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Creates a new VFX context (Spawn, Initialize, Update, Output) in a graph"
)
async def create_context(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    context_type: Annotated[str, "Type of context to create (e.g., 'VFXBasicSpawner', 'VFXBasicInitialize')"],
    position_x: Annotated[float, "X position for the context"],
    position_y: Annotated[float, "Y position for the context"],
) -> dict[str, Any]:
    await ctx.info(f"Creating context '{context_type}' at ({position_x}, {position_y}) in graph {graph_path}")
    
    params = {
        "graph_path": graph_path,
        "context_type": context_type,
        "position_x": position_x,
        "position_y": position_y,
    }
    
    response = send_command_with_retry("create_context", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

