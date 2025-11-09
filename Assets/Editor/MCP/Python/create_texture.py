from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Create a placeholder texture asset"
)
async def create_texture(
    ctx: Context,
    texture_path: Annotated[str, "Path for texture asset (e.g., 'Assets/Textures/MyTexture.png')"],
    width: Annotated[int, "Texture width in pixels"] = 256,
    height: Annotated[int, "Texture height in pixels"] = 256,
    texture_format: Annotated[str, "Texture format (e.g., 'RGBA32', 'RGB24')"] = "RGBA32",
    fill_color: Annotated[dict[str, float] | None, "Fill color as {r, g, b, a} (0-1)"] = None,
    replace_existing: Annotated[bool, "Overwrite if texture already exists"] = False,
) -> dict[str, Any]:
    await ctx.info(f"Creating texture at {texture_path} ({width}x{height})")
    
    params = {
        "texture_path": texture_path,
        "width": width,
        "height": height,
        "texture_format": texture_format,
        "replace_existing": replace_existing,
    }
    
    if fill_color is not None:
        params["fill_color"] = fill_color
    
    response = send_command_with_retry("create_texture", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

