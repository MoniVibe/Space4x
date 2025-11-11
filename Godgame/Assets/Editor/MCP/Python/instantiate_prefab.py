from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Instantiate a prefab in the current scene"
)
async def instantiate_prefab(
    ctx: Context,
    prefab_path: Annotated[str, "Path to prefab asset"],
    position_x: Annotated[float, "X position"] = 0.0,
    position_y: Annotated[float, "Y position"] = 0.0,
    position_z: Annotated[float, "Z position"] = 0.0,
    parent_name: Annotated[str | None, "Optional parent GameObject name"] = None,
) -> dict[str, Any]:
    await ctx.info(f"Instantiating prefab {prefab_path} at ({position_x}, {position_y}, {position_z})")
    
    params = {
        "prefab_path": prefab_path,
        "position_x": position_x,
        "position_y": position_y,
        "position_z": position_z,
    }
    if parent_name is not None:
        params["parent_name"] = parent_name
    
    response = send_command_with_retry("instantiate_prefab", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

