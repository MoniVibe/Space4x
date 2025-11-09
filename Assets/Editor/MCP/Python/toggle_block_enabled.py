from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Enables or disables a block within a VFX context"
)
async def toggle_block_enabled(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    context_id: Annotated[int, "ID of the context containing the block"],
    block_id: Annotated[int, "ID of the block to toggle"],
    enabled: Annotated[bool, "Whether the block should be enabled (true) or disabled (false)"],
) -> dict[str, Any]:
    await ctx.info(f"{'Enabling' if enabled else 'Disabling'} block {block_id} in context {context_id} in graph {graph_path}")
    
    params = {
        "graph_path": graph_path,
        "context_id": context_id,
        "block_id": block_id,
        "enabled": enabled,
    }
    
    response = send_command_with_retry("toggle_block_enabled", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

