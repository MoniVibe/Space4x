from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Render a preview of a VFX graph with specified parameters"
)
async def render_vfx_preview(
    ctx: Context,
    graph_path: Annotated[str, "Path to the VFX graph asset"] = None,
    graph_id: Annotated[str, "Graph ID (filename without extension)"] = None,
    params: Annotated[dict[str, Any], "Parameter values to apply"] = None,
    size: Annotated[int, "Preview size in pixels (width=height)"] = 320,
    seconds: Annotated[float, "Duration to render in seconds"] = 0.5,
    fps: Annotated[int, "Frames per second"] = 16,
    frames: Annotated[int, "Number of frames to render (1 for single frame)"] = 1,
) -> dict[str, Any]:
    if not graph_path and not graph_id:
        return {"success": False, "message": "Either graph_path or graph_id is required"}
    
    await ctx.info(f"Rendering VFX preview: {graph_path or graph_id}")
    
    request_params = {}
    if graph_path:
        request_params["graph_path"] = graph_path
    if graph_id:
        request_params["graph_id"] = graph_id
    if params:
        request_params["params"] = params
    request_params["size"] = size
    request_params["seconds"] = seconds
    request_params["fps"] = fps
    request_params["frames"] = frames
    
    response = send_command_with_retry("render_vfx_preview", request_params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

