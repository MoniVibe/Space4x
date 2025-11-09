from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Get project metadata including project name, Unity version, package list, available graph systems, etc."
)
async def get_project_info(
    ctx: Context,
) -> dict[str, Any]:
    await ctx.info("Getting project information...")
    
    response = send_command_with_retry("get_project_info", {})
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

