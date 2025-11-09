from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Create a ScriptableObject asset instance"
)
async def create_scriptable_object_instance(
    ctx: Context,
    script_type: Annotated[str, "Full type name (e.g., 'Space4X.Registry.Space4XCameraProfile')"],
    asset_path: Annotated[str, "Path for asset (e.g., 'Assets/Data/CameraProfile.asset')"],
    property_values: Annotated[dict[str, Any] | None, "Property values dictionary"] = None,
) -> dict[str, Any]:
    await ctx.info(f"Creating ScriptableObject instance {script_type} at {asset_path}")
    
    params = {
        "script_type": script_type,
        "asset_path": asset_path,
    }
    if property_values is not None:
        params["property_values"] = property_values
    
    response = send_command_with_retry("create_scriptable_object_instance", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

