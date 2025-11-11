from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Create a new material asset"
)
async def create_material(
    ctx: Context,
    material_path: Annotated[str, "Path for material asset (e.g., 'Assets/Materials/MyMaterial.mat')"],
    shader_name: Annotated[str, "Shader name (e.g., 'Standard', 'Unlit/Color')"] = "Standard",
    replace_existing: Annotated[bool, "Overwrite if material already exists"] = False,
) -> dict[str, Any]:
    await ctx.info(f"Creating material at {material_path} with shader {shader_name}")
    
    params = {
        "material_path": material_path,
        "shader_name": shader_name,
        "replace_existing": replace_existing,
    }
    
    response = send_command_with_retry("create_material", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

