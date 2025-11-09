from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Get transform properties (position, rotation, scale, parent, children) from a GameObject"
)
async def get_transform(
    ctx: Context,
    gameobject_name: Annotated[str, "Name or instance ID of GameObject"],
    search_method: Annotated[str, "Search method: 'by_name', 'by_id'"] = "by_name",
) -> dict[str, Any]:
    await ctx.info(f"Getting transform from {gameobject_name}")
    
    params = {
        "gameobject_name": gameobject_name,
        "search_method": search_method,
    }
    
    response = send_command_with_retry("get_transform", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

