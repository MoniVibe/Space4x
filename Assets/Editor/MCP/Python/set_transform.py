from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Set transform properties (position, rotation, scale, parent) on a GameObject"
)
async def set_transform(
    ctx: Context,
    gameobject_name: Annotated[str, "Name or instance ID of GameObject"],
    position: Annotated[dict[str, float] | None, "Position as {x, y, z}"] = None,
    rotation: Annotated[dict[str, float] | None, "Rotation (Euler angles) as {x, y, z}"] = None,
    scale: Annotated[dict[str, float] | None, "Scale as {x, y, z}"] = None,
    parent_name: Annotated[str | None, "Name of parent GameObject (empty string to unparent)"] = None,
    search_method: Annotated[str, "Search method: 'by_name', 'by_id'"] = "by_name",
) -> dict[str, Any]:
    await ctx.info(f"Setting transform on {gameobject_name}")
    
    params = {
        "gameobject_name": gameobject_name,
        "search_method": search_method,
    }
    
    if position is not None:
        params["position"] = position
    if rotation is not None:
        params["rotation"] = rotation
    if scale is not None:
        params["scale"] = scale
    if parent_name is not None:
        params["parent_name"] = parent_name
    
    response = send_command_with_retry("set_transform", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

