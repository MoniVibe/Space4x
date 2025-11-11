from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Collect VFX dataset by sampling parameters and rendering previews. Runs entirely inside Unity, bypassing socket connection issues."
)
async def collect_vfx_dataset(
    ctx: Context,
    graph_id: Annotated[str, "Graph ID (filename without extension)"] = None,
    graph_path: Annotated[str, "Path to VFX graph asset"] = None,
    samples: Annotated[int, "Number of parameter samples to collect"] = 32,
    output_dir: Annotated[str, "Output directory relative to project root"] = "Data/MCP_Exports",
    size: Annotated[int, "Preview image size (width=height)"] = 320,
    seconds: Annotated[float, "Duration to render in seconds"] = 0.5,
    fps: Annotated[int, "Frames per second"] = 16,
    seed: Annotated[int, "Random seed for parameter sampling"] = 0,
) -> dict[str, Any]:
    """
    Collect VFX dataset by:
    1. Getting graph descriptor (exposed parameters)
    2. Sampling random parameter values
    3. Rendering previews for each sample
    4. Saving CSV, JSON descriptor, and PNG frames to disk
    
    This tool runs entirely inside Unity, so it bypasses socket connection issues.
    Python scripts can then read the exported data for embedding generation and training.
    """
    await ctx.info(f"Collecting VFX dataset for graph: {graph_id or graph_path}")
    
    if not graph_id and not graph_path:
        return {"success": False, "message": "Either graph_id or graph_path is required"}
    
    params = {}
    if graph_id:
        params["graph_id"] = graph_id
    if graph_path:
        params["graph_path"] = graph_path
    params["samples"] = samples
    params["output_dir"] = output_dir
    params["size"] = size
    params["seconds"] = seconds
    params["fps"] = fps
    params["seed"] = seed
    
    response = send_command_with_retry("collect_vfx_dataset", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

