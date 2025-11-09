from typing import Annotated, Any

from fastmcp import Context

from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry


@mcp_for_unity_tool(
    description="Create a starter VFX graph with spawn/initialize/update/output contexts and basic blocks."
)
async def build_vfx_graph_tree(
    ctx: Context,
    graph_path: Annotated[str, "Path to the VFX graph asset to create or update (e.g. 'Assets/VFX/MyGraph.vfx')"],
    overwrite: Annotated[bool, "Overwrite the graph asset if it already exists"] = False,
    spawn_rate: Annotated[float, "Spawn rate to apply to the constant spawn block"] = 32.0,
) -> dict[str, Any]:
    await ctx.info(
        f"Building VFX graph tree at {graph_path} "
        f"(overwrite={'yes' if overwrite else 'no'}, spawn_rate={spawn_rate})"
    )

    params: dict[str, Any] = {
        "graph_path": graph_path,
        "overwrite": overwrite,
        "spawn_rate": spawn_rate,
    }

    response = send_command_with_retry("build_vfx_graph_tree", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}


