"""
Headless VFX capture using EditorApplication updates
Works without Play Mode and doesn't freeze the editor
"""
import os
import subprocess
import json
import asyncio
from pathlib import Path

from .mcp_for_unity_tool import mcp_for_unity_tool, send_command_with_retry


@mcp_for_unity_tool(
    description="Capture VFX using headless mode with EditorApplication updates. Works without Play Mode and doesn't freeze the editor."
)
async def capture_vfx_headless(
    ctx: Context,
    graph_id: Annotated[str, "Graph ID (filename without extension)"] = None,
    graph_path: Annotated[str, "Path to the VFX graph asset"] = None,
    width: Annotated[int, "Capture width in pixels"] = 512,
    height: Annotated[int, "Capture height in pixels"] = 512,
    duration: Annotated[float, "Duration to capture in seconds"] = 3.0,
    frame_count: Annotated[int, "Number of frames to capture"] = 10,
    background_color: Annotated[str, "Background color: 'black' or 'white'"] = "black",
    output_dir: Annotated[str, "Output directory for captures"] = "Data/MCP_Exports/MultiAngle",
) -> dict[str, Any]:
    """
    Capture VFX using headless mode with EditorApplication updates
    Works without Play Mode and doesn't freeze the editor
    """
    if not graph_path and not graph_id:
        return {"success": False, "message": "Either graph_path or graph_id is required"}
    
    await ctx.info(f"Capturing VFX headless: {graph_path or graph_id}")
    
    try:
        # Try direct MCP call (if Unity is running)
        request_params = {}
        if graph_path:
            request_params["graph_path"] = graph_path
        if graph_id:
            request_params["graph_id"] = graph_id
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
        
        response = await send_command_with_retry("capture_vfx_headless", request_params)
        return response if isinstance(response, dict) else {"success": False, "message": str(response)}
    except Exception as e:
        return {
            "success": False,
            "error": f"Failed to capture VFX headless: {str(e)}"
        }

