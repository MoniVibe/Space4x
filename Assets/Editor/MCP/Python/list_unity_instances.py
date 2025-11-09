from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="List all available Unity instances with their identifiers, names, ports, and active scenes."
)
async def list_unity_instances(
    ctx: Context,
) -> dict[str, Any]:
    await ctx.info("Listing Unity instances...")
    
    response = send_command_with_retry("list_unity_instances", {})
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

