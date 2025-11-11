from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Plays a visual effect (starts playback)"
)
async def play_effect(
    ctx: Context,
    gameobject_name: Annotated[str, "Name of the GameObject with the VisualEffect component"],
) -> dict[str, Any]:
    await ctx.info(f"Playing effect on {gameobject_name}")
    
    params = {
        "gameobject_name": gameobject_name,
    }
    
    response = send_command_with_retry("play_effect", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

