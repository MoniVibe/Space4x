from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Spawns a prefab with a VisualEffect component in the scene"
)
async def spawn_visual_effect_prefab(
    ctx: Context,
    prefab_path: Annotated[str, "Path to the prefab asset"],
    position_x: Annotated[float | None, "X position (default: 0)"] = None,
    position_y: Annotated[float | None, "Y position (default: 0)"] = None,
    position_z: Annotated[float | None, "Z position (default: 0)"] = None,
    parent_name: Annotated[str | None, "Optional parent GameObject name"] = None,
) -> dict[str, Any]:
    await ctx.info(f"Spawning visual effect prefab from {prefab_path}")
    
    params = {
        "prefab_path": prefab_path,
        "position_x": position_x if position_x is not None else 0,
        "position_y": position_y if position_y is not None else 0,
        "position_z": position_z if position_z is not None else 0,
    }
    if parent_name is not None:
        params["parent_name"] = parent_name
    
    response = send_command_with_retry("spawn_visual_effect_prefab", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

