from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Get a lightweight descriptor for a VFX graph (exposed params, stages, tags)"
)
async def describe_vfx_graph(
    ctx: Context,
    graph_path: Annotated[str, "Path to the VFX graph asset (e.g., 'Assets/VFX/MyGraph.vfx')"] = None,
    graph_id: Annotated[str, "Graph ID (filename without extension)"] = None,
    lod: Annotated[int, "Level of detail (0=minimal, 2=full, default=2)"] = 2,
) -> dict[str, Any]:
    if not graph_path and not graph_id:
        return {"success": False, "message": "Either graph_path or graph_id is required"}
    
    await ctx.info(f"Describing VFX graph: {graph_path or graph_id}")
    
    params = {}
    if graph_path:
        params["graph_path"] = graph_path
    if graph_id:
        params["graph_id"] = graph_id
    params["lod"] = lod
    
    response = send_command_with_retry("describe_vfx_graph", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

