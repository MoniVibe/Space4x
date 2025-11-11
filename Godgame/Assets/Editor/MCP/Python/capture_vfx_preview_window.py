from typing import Annotated

from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry


@mcp_for_unity_tool(
    description="Capture a screenshot of the currently opened VFX Graph preview window. Can open the graph automatically and ensure the preview is playing."
)
async def capture_vfx_preview_window(
    ctx: Context,
    graph_id: Annotated[str, "Graph ID (filename without extension)"] | None = None,
    graph_path: Annotated[str, "Path to VFX graph asset"] | None = None,
    auto_play: Annotated[bool, "Ensure preview is playing before capture"] = True,
    output_dir: Annotated[str, "Output directory relative to project root"] = "Data/MCP_Exports",
    file_name: Annotated[str, "Base filename without extension"] | None = None,
) -> dict[str, object]:
    """Capture the VFX preview window as a PNG image."""
    params: dict[str, object] = {
        "auto_play": auto_play,
        "output_dir": output_dir,
    }
    if graph_id:
        params["graph_id"] = graph_id
    if graph_path:
        params["graph_path"] = graph_path
    if file_name:
        params["file_name"] = file_name

    response = send_command_with_retry("capture_vfx_preview_window", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}
