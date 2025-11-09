from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Get all components on a GameObject with their properties. Useful for debugging and verification."
)
async def get_component_info(
    ctx: Context,
    gameobject_name: Annotated[str, "Name or instance ID of the GameObject"],
    search_method: Annotated[str, "Search method: 'by_name', 'by_id'"] = "by_name",
    include_hidden: Annotated[bool, "Include hidden/internal components"] = False,
) -> dict[str, Any]:
    await ctx.info(f"Getting component info for {gameobject_name}")
    
    params = {
        "action": "get_info",
        "gameobject_name": gameobject_name,
        "search_method": search_method,
        "include_hidden": include_hidden,
    }
    
    response = send_command_with_retry("get_component_info", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

