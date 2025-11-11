from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Swaps the VFX graph asset on a VisualEffect component"
)
async def swap_visual_effect_graph(
    ctx: Context,
    gameobject_name: Annotated[str, "Name of the GameObject with the VisualEffect component"],
    graph_path: Annotated[str, "Path to the new VFX graph asset"],
) -> dict[str, Any]:
    await ctx.info(f"Swapping VFX graph on {gameobject_name} to {graph_path}")
    
    params = {
        "gameobject_name": gameobject_name,
        "graph_path": graph_path,
    }
    
    response = send_command_with_retry("swap_visual_effect_graph", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

