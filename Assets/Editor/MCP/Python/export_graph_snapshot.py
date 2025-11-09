from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Exports a snapshot of the graph structure to JSON for comparison or backup"
)
async def export_graph_snapshot(
    ctx: Context,
    graph_path: Annotated[str, "Path to graph asset"],
    snapshot_path: Annotated[str | None, "Path where snapshot should be saved (default: graph_path with _snapshot.json)"] = None,
) -> dict[str, Any]:
    await ctx.info(f"Exporting graph snapshot for {graph_path}")
    
    params = {
        "graph_path": graph_path,
    }
    if snapshot_path is not None:
        params["snapshot_path"] = snapshot_path
    
    response = send_command_with_retry("export_graph_snapshot", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

