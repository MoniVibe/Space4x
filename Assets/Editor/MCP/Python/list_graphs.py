from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="List all VFX graph assets in the project"
)
async def list_graphs(
    ctx: Context,
    include_tags: Annotated[bool, "Include tags derived from folder hierarchy"] = True,
) -> dict[str, Any]:
    await ctx.info("Listing VFX graphs")
    
    params = {
        "include_tags": include_tags,
    }
    
    response = send_command_with_retry("list_graphs", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

