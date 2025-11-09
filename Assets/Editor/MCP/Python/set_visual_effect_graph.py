from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Sets the VFX graph asset on a VisualEffect component in the scene"
)
async def set_visual_effect_graph(
    ctx: Context,
    gameobject_name: Annotated[str, "Name of the GameObject with a VisualEffect component"],
    graph_path: Annotated[str, "Path to the VFX graph asset to assign"],
) -> dict[str, Any]:
    await ctx.info(f"Setting VFX graph {graph_path} on {gameobject_name}")
    
    params = {
        "gameobject_name": gameobject_name,
        "graph_path": graph_path,
    }
    
    response = send_command_with_retry("set_visual_effect_graph", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

