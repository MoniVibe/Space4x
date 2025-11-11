from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Read structure of a Unity graph (Shader Graph, VFX Graph, Visual Scripting, etc.)"
)
async def get_graph_structure(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset (e.g., 'Assets/Shaders/MyShader.shadergraph')"],
) -> dict[str, Any]:
    await ctx.info(f"Reading graph structure: {graph_path}")
    
    params = {
        "graph_path": graph_path,
    }
    
    response = send_command_with_retry("get_graph_structure", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

