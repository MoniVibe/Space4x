from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Unbinds a VFX parameter, resetting it to its default value"
)
async def unbind_parameter(
    ctx: Context,
    gameobject_name: Annotated[str, "Name of the GameObject with the VisualEffect component"],
    parameter_name: Annotated[str, "Name of the VFX parameter to unbind"],
) -> dict[str, Any]:
    await ctx.info(f"Unbinding parameter '{parameter_name}' from {gameobject_name}")
    
    params = {
        "gameobject_name": gameobject_name,
        "parameter_name": parameter_name,
    }
    
    response = send_command_with_retry("unbind_parameter", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

