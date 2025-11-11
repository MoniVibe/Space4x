from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Remove a block from a VFX context"
)
async def remove_block_from_context(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    context_id: Annotated[str | int, "Context node ID"],
    block_id: Annotated[str | int, "Block instance ID"],
) -> dict[str, Any]:
    await ctx.info(f"Removing block {block_id} from context {context_id} in graph {graph_path}")
    
    params = {
        "graph_path": graph_path,
        "context_id": context_id,
        "block_id": block_id,
    }
    
    response = send_command_with_retry("remove_block_from_context", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

