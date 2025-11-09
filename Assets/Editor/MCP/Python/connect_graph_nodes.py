from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Connect two nodes in a Unity graph"
)
async def connect_graph_nodes(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    source_node_id: Annotated[str, "Source node ID or name"],
    source_port: Annotated[str, "Output port name on source node"],
    target_node_id: Annotated[str, "Target node ID or name"],
    target_port: Annotated[str, "Input port name on target node"],
) -> dict[str, Any]:
    await ctx.info(f"Connecting {source_node_id}.{source_port} -> {target_node_id}.{target_port} in {graph_path}")
    
    params = {
        "graph_path": graph_path,
        "source_node_id": source_node_id,
        "source_port": source_port,
        "target_node_id": target_node_id,
        "target_port": target_port,
    }
    
    response = send_command_with_retry("connect_graph_nodes", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

