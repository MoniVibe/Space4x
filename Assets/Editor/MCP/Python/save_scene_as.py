from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Save current scene with new name/path"
)
async def save_scene_as(
    ctx: Context,
    scene_path: Annotated[str, "New scene path (e.g., 'Assets/Scenes/Demo/MyScene.unity')"],
    create_backup: Annotated[bool, "Create backup if scene already exists"] = True,
) -> dict[str, Any]:
    await ctx.info(f"Saving scene as {scene_path}")
    
    params = {
        "scene_path": scene_path,
        "create_backup": create_backup,
    }
    
    response = send_command_with_retry("save_scene_as", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

