from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Set a property value on a node in a Unity graph"
)
async def set_graph_node_property(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    node_id: Annotated[str | int, "Node ID"],
    property_name: Annotated[str, "Property name to set"],
    property_value: Annotated[Any, "Property value (will be converted to appropriate type)"],
) -> dict[str, Any]:
    await ctx.info(f"Setting property {property_name}={property_value} on node {node_id} in graph {graph_path}")
    
    params = {
        "graph_path": graph_path,
        "node_id": node_id,
        "property_name": property_name,
        "property_value": property_value,
    }
    
    response = send_command_with_retry("set_graph_node_property", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

