from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Identify which Unity project/instance we're currently connected to. Returns project name, scene path, project root, and instance identifier."
)
async def identify_unity_instance(
    ctx: Context,
) -> dict[str, Any]:
    await ctx.info("Identifying Unity instance...")
    
    response = send_command_with_retry("identify_unity_instance", {})
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

