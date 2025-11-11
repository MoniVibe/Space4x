from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Remove a scripting define symbol from a build target group"
)
async def remove_scripting_define(
    ctx: Context,
    define: Annotated[str, "Scripting define symbol to remove"],
    target_group: Annotated[str | None, "Build target group (e.g., 'Standalone', 'Android', 'iOS')"] = None,
) -> dict[str, Any]:
    await ctx.info(f"Removing scripting define '{define}' from {target_group or 'Standalone'}")
    
    params = {
        "define": define,
    }
    
    if target_group is not None:
        params["target_group"] = target_group
    
    response = send_command_with_retry("remove_scripting_define", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

