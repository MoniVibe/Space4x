from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="List all blocks in a VFX context (Initialize, Update, or Output)"
)
async def list_context_blocks(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    context_id: Annotated[str | int, "Context node ID"],
) -> dict[str, Any]:
    await ctx.info(f"Listing blocks in context {context_id} in graph {graph_path}")
    
    params = {
        "graph_path": graph_path,
        "context_id": context_id,
    }
    
    response = send_command_with_retry("list_context_blocks", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

