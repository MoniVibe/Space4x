from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Sends a VFX event to a visual effect (e.g., 'Play', 'Stop', custom events)"
)
async def send_vfx_event(
    ctx: Context,
    gameobject_name: Annotated[str, "Name of the GameObject with the VisualEffect component"],
    event_name: Annotated[str, "Name of the VFX event to send"],
) -> dict[str, Any]:
    await ctx.info(f"Sending VFX event '{event_name}' to {gameobject_name}")
    
    params = {
        "gameobject_name": gameobject_name,
        "event_name": event_name,
    }
    
    response = send_command_with_retry("send_vfx_event", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

