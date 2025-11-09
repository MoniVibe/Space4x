from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Binds a VFX parameter to a property on a GameObject in the scene"
)
async def bind_parameter_to_object(
    ctx: Context,
    gameobject_name: Annotated[str, "Name of the GameObject with the VisualEffect component"],
    parameter_name: Annotated[str, "Name of the VFX parameter to bind"],
    target_object_name: Annotated[str, "Name of the target GameObject to bind to"],
    property_path: Annotated[str, "Property path on the target object (e.g., 'transform.position')"],
) -> dict[str, Any]:
    await ctx.info(f"Binding parameter '{parameter_name}' on {gameobject_name} to {target_object_name}.{property_path}")
    
    params = {
        "gameobject_name": gameobject_name,
        "parameter_name": parameter_name,
        "target_object_name": target_object_name,
        "property_path": property_path,
    }
    
    response = send_command_with_retry("bind_parameter_to_object", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

