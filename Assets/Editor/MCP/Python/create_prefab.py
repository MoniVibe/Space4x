from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Create a prefab from a GameObject in the scene"
)
async def create_prefab(
    ctx: Context,
    gameobject_name: Annotated[str, "Name or instance ID of GameObject to convert"],
    prefab_path: Annotated[str, "Path for prefab asset (e.g., 'Assets/Prefabs/Carrier.prefab')"],
    replace_existing: Annotated[bool, "Overwrite if prefab already exists"] = False,
    search_method: Annotated[str, "Search method: 'by_name', 'by_id', 'by_path'"] = "by_name",
) -> dict[str, Any]:
    await ctx.info(f"Creating prefab from {gameobject_name} at {prefab_path}")
    
    params = {
        "gameobject_name": gameobject_name,
        "prefab_path": prefab_path,
        "replace_existing": replace_existing,
        "search_method": search_method,
    }
    
    response = send_command_with_retry("create_prefab", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

