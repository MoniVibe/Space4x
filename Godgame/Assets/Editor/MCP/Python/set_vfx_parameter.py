from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Sets a parameter value on a VisualEffect component (supports float, int, bool, Vector2/3/4, Color, Texture2D)"
)
async def set_vfx_parameter(
    ctx: Context,
    gameobject_name: Annotated[str, "Name of the GameObject with the VisualEffect component"],
    parameter_name: Annotated[str, "Name of the VFX parameter"],
    parameter_value: Annotated[Any, "Value to set (auto-detects type, or specify parameter_type)"],
    parameter_type: Annotated[str | None, "Optional parameter type (float, int, bool, vector2, vector3, vector4, color, texture2d)"] = None,
) -> dict[str, Any]:
    await ctx.info(f"Setting parameter '{parameter_name}' on {gameobject_name}")
    
    params = {
        "gameobject_name": gameobject_name,
        "parameter_name": parameter_name,
        "parameter_value": parameter_value,
    }
    if parameter_type is not None:
        params["parameter_type"] = parameter_type
    
    response = send_command_with_retry("set_vfx_parameter", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

