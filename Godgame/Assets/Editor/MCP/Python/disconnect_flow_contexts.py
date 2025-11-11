from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Disconnect two VFX contexts via flow"
)
async def disconnect_flow_contexts(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    source_context_id: Annotated[str | int, "Source context node ID"],
    source_slot_index: Annotated[int | None, "Source flow slot index (default: 0)"] = None,
    target_context_id: Annotated[str | int, "Target context node ID"],
    target_slot_index: Annotated[int | None, "Target flow slot index (default: 0)"] = None,
) -> dict[str, Any]:
    await ctx.info(f"Disconnecting flow contexts {source_context_id}:{source_slot_index} -> {target_context_id}:{target_slot_index} in {graph_path}")
    
    params = {
        "graph_path": graph_path,
        "source_context_id": source_context_id,
        "target_context_id": target_context_id,
    }
    if source_slot_index is not None:
        params["source_slot_index"] = source_slot_index
    if target_slot_index is not None:
        params["target_slot_index"] = target_slot_index
    
    response = send_command_with_retry("disconnect_flow_contexts", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

