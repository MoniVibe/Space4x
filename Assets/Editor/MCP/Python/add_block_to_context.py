from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Add a block to a VFX context"
)
async def add_block_to_context(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    context_id: Annotated[str | int, "Context node ID"],
    block_type: Annotated[str, "Block type identifier (e.g., 'SetAttribute', 'Orient')"],
    insert_index: Annotated[int | None, "Index to insert block at (optional)"] = None,
) -> dict[str, Any]:
    await ctx.info(f"Adding block {block_type} to context {context_id} in graph {graph_path}")
    
    params = {
        "graph_path": graph_path,
        "context_id": context_id,
        "block_type": block_type,
    }
    if insert_index is not None:
        params["insert_index"] = insert_index
    
    response = send_command_with_retry("add_block_to_context", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

