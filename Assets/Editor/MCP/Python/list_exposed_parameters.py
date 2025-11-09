from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="List all exposed parameters in a VFX graph"
)
async def list_exposed_parameters(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
) -> dict[str, Any]:
    await ctx.info(f"Listing exposed parameters in graph {graph_path}")
    
    params = {
        "graph_path": graph_path,
    }
    
    response = send_command_with_retry("list_exposed_parameters", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

