from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Refresh and save all assets in the Unity project"
)
async def refresh_assets(
    ctx: Context,
) -> dict[str, Any]:
    await ctx.info("Refreshing assets")
    
    params = {}
    
    response = send_command_with_retry("refresh_assets", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

