from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Deletes an exposed parameter from a VFX graph"
)
async def delete_exposed_parameter(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    parameter_name: Annotated[str, "Name of the parameter to delete"],
) -> dict[str, Any]:
    await ctx.info(f"Deleting parameter '{parameter_name}' from graph {graph_path}")
    
    params = {
        "graph_path": graph_path,
        "parameter_name": parameter_name,
    }
    
    response = send_command_with_retry("delete_exposed_parameter", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

