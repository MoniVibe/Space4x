from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Create a new exposed parameter in a VFX graph"
)
async def create_exposed_parameter(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    parameter_name: Annotated[str, "Parameter name"],
    parameter_type: Annotated[str, "Parameter type (e.g., 'Float', 'Vector3', 'Texture2D')"],
    default_value: Annotated[Any | None, "Default value (optional)"] = None,
) -> dict[str, Any]:
    await ctx.info(f"Creating exposed parameter {parameter_name} of type {parameter_type} in graph {graph_path}")
    
    params = {
        "graph_path": graph_path,
        "parameter_name": parameter_name,
        "parameter_type": parameter_type,
    }
    if default_value is not None:
        params["default_value"] = default_value
    
    response = send_command_with_retry("create_exposed_parameter", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

