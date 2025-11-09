from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Create a primitive mesh asset (plane, cube, sphere, cylinder)"
)
async def create_mesh(
    ctx: Context,
    mesh_path: Annotated[str, "Path for mesh asset (e.g., 'Assets/Meshes/MyMesh.asset')"],
    mesh_type: Annotated[str, "Mesh type: 'plane', 'cube', 'sphere', 'cylinder'"] = "plane",
    replace_existing: Annotated[bool, "Overwrite if mesh already exists"] = False,
) -> dict[str, Any]:
    await ctx.info(f"Creating {mesh_type} mesh at {mesh_path}")
    
    params = {
        "mesh_path": mesh_path,
        "mesh_type": mesh_type,
        "replace_existing": replace_existing,
    }
    
    response = send_command_with_retry("create_mesh", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

