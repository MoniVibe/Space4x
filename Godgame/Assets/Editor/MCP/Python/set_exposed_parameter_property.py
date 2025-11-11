from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Sets a property on an exposed parameter (e.g., category, tooltip, exposedName)"
)
async def set_exposed_parameter_property(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    parameter_name: Annotated[str, "Name of the parameter"],
    property_name: Annotated[str, "Name of the property to set (e.g., 'exposedName', 'category', 'tooltip')"],
    property_value: Annotated[Any, "Value to set for the property"],
) -> dict[str, Any]:
    await ctx.info(f"Setting property '{property_name}' on parameter '{parameter_name}' in graph {graph_path}")
    
    params = {
        "graph_path": graph_path,
        "parameter_name": parameter_name,
        "property_name": property_name,
        "property_value": property_value,
    }
    
    response = send_command_with_retry("set_exposed_parameter_property", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

