from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Capture VFX from multiple camera angles over time. Creates a capture rig with 8 cameras (front/back/left/right/top/bottom + diagonals) and captures frames over a duration."
)
async def capture_vfx_multi_angle(
    ctx: Context,
    graph_id: Annotated[str, "Graph ID (filename without extension)"] = None,
    graph_path: Annotated[str, "Path to the VFX graph asset"] = None,
    vfx_instance_name: Annotated[str, "Name of the VFX instance GameObject in scene"] = "SandboxVFX",
    width: Annotated[int, "Capture width in pixels"] = 512,
    height: Annotated[int, "Capture height in pixels"] = 512,
    duration: Annotated[float, "Duration to capture in seconds"] = 5.0,
    frame_count: Annotated[int, "Number of frames to capture"] = 10,
    background_color: Annotated[str, "Background color: 'black' or 'white'"] = "black",
    output_dir: Annotated[str, "Output directory for captures"] = "Data/MCP_Exports/MultiAngle",
) -> dict[str, Any]:
    if not graph_path and not graph_id:
        return {"success": False, "message": "Either graph_path or graph_id is required"}
    
    await ctx.info(f"Capturing multi-angle VFX: {graph_path or graph_id}")
    
    request_params = {}
    if graph_path:
        request_params["graph_path"] = graph_path
    if graph_id:
        request_params["graph_id"] = graph_id
    if vfx_instance_name:
        request_params["vfx_instance_name"] = vfx_instance_name
    if width:
        request_params["width"] = width
    if height:
        request_params["height"] = height
    if duration:
        request_params["duration"] = duration
    if frame_count:
        request_params["frame_count"] = frame_count
    if background_color:
        request_params["background_color"] = background_color
    if output_dir:
        request_params["output_dir"] = output_dir
    
    response = send_command_with_retry("capture_vfx_multi_angle", request_params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

