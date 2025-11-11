from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Switch to a specific Unity instance. Provide instance identifier (name, hash, or name@hash format)."
)
async def switch_unity_instance(
    ctx: Context,
    instance: Annotated[str, "Instance identifier (name, hash, or name@hash format)"],
) -> dict[str, Any]:
    await ctx.info(f"Switching to Unity instance: {instance}")
    
    params = {
        "action": "switch",
        "instance": instance,
    }
    
    response = send_command_with_retry("switch_unity_instance", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

