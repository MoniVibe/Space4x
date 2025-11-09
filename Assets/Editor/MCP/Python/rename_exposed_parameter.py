from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Renames an exposed parameter in a VFX graph"
)
async def rename_exposed_parameter(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    parameter_name: Annotated[str, "Current name of the parameter"],
    new_name: Annotated[str, "New name for the parameter"],
) -> dict[str, Any]:
    await ctx.info(f"Renaming parameter '{parameter_name}' to '{new_name}' in graph {graph_path}")
    
    params = {
        "graph_path": graph_path,
        "parameter_name": parameter_name,
        "new_name": new_name,
    }
    
    response = send_command_with_retry("rename_exposed_parameter", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

