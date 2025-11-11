from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Find assets by type"
)
async def find_assets_by_type(
    ctx: Context,
    asset_type: Annotated[str, "Asset type name (e.g., 'Prefab', 'Material', 'Texture2D', 'ScriptableObject')"],
    search_in_subfolders: Annotated[bool, "Search recursively"] = True,
) -> dict[str, Any]:
    await ctx.info(f"Finding assets of type {asset_type}")
    
    params = {
        "asset_type": asset_type,
        "search_in_subfolders": search_in_subfolders,
    }
    
    response = send_command_with_retry("find_assets_by_type", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

