from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="List scripting define symbols for a build target group"
)
async def list_scripting_defines(
    ctx: Context,
    target_group: Annotated[str | None, "Build target group (e.g., 'Standalone', 'Android', 'iOS')"] = None,
) -> dict[str, Any]:
    await ctx.info(f"Listing scripting defines for {target_group or 'Standalone'}")
    
    params = {}
    
    if target_group is not None:
        params["target_group"] = target_group
    
    response = send_command_with_retry("list_scripting_defines", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

