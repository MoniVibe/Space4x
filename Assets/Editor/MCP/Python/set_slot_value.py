from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Set the default value of a slot (port) on a node"
)
async def set_slot_value(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    node_id: Annotated[str | int, "Node instance ID"],
    slot_name: Annotated[str, "Slot name or path"],
    slot_value: Annotated[Any, "Value to set (will be converted to appropriate type)"],
) -> dict[str, Any]:
    await ctx.info(f"Setting slot {slot_name} value on node {node_id} in graph {graph_path}")
    
    params = {
        "graph_path": graph_path,
        "node_id": node_id,
        "slot_name": slot_name,
        "slot_value": slot_value,
    }
    
    response = send_command_with_retry("set_slot_value", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

