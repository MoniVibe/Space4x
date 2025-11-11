from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Duplicates an existing VFX graph asset to a new path"
)
async def duplicate_graph(
    ctx: Context,
    source_path: Annotated[str, "Path to the source VFX graph asset"],
    destination_path: Annotated[str, "Path where the duplicated graph should be created"],
) -> dict[str, Any]:
    await ctx.info(f"Duplicating graph from {source_path} to {destination_path}")
    
    params = {
        "source_path": source_path,
        "destination_path": destination_path,
    }
    
    response = send_command_with_retry("duplicate_graph", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

