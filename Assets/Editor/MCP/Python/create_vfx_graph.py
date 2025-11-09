from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Creates a new VFX graph asset at the specified path"
)
async def create_vfx_graph(
    ctx: Context,
    graph_path: Annotated[str, "Path where the new VFX graph asset should be created (must end with .vfx)"],
) -> dict[str, Any]:
    await ctx.info(f"Creating VFX graph at {graph_path}")

    params: dict[str, Any] = {
        "graph_path": graph_path,
    }

    response = send_command_with_retry("create_vfx_graph", params)

    if isinstance(response, dict) and response.get("success"):
        return response

    message = ""
    if isinstance(response, dict):
        message = str(response.get("message", ""))
        if "Unknown or unsupported command type" not in message:
            return response
    else:
        message = str(response)

    await ctx.warn(
        "create_vfx_graph command not available on MCP server; attempting template fallback"
    )

    template_candidates = [
        "Packages/com.unity.visualeffectgraph/Editor/DefaultResources/New VFX.vfx",
        "Packages/com.unity.visualeffectgraph/Editor/DefaultResources/New VFX Graph.vfx",
        "Assets/Samples/Visual Effect Graph/17.2.0/Learning Templates/VFX/New VFX.vfx",
        "Assets/Samples/Visual Effect Graph/17.2.0/VisualEffectGraph Additions/VFX/Bonfire.vfx",
    ]
    
    for template_path in template_candidates:
        duplicate_response = send_command_with_retry(
            "duplicate_graph",
            {
                "source_path": template_path,
                "destination_path": graph_path,
            },
        )

        if isinstance(duplicate_response, dict) and duplicate_response.get("success"):
            return {
                "success": True,
                "message": f"VFX graph created at {graph_path} via template '{template_path}'",
                "data": {
                    "graphPath": graph_path,
                    "templatePath": template_path,
                },
            }

    await ctx.warn(
        "Template fallback failed; attempting build_vfx_graph_tree fallback"
    )

    tree_response = send_command_with_retry(
        "build_vfx_graph_tree",
        {
            "graph_path": graph_path,
            "spawn_rate": 0.0,
        },
    )

    if isinstance(tree_response, dict):
        if tree_response.get("success"):
            tree_response["message"] = (
                tree_response.get("message", "")
                + " (fallback via build_vfx_graph_tree)"
            ).strip()
        return tree_response

    return {
        "success": False,
        "message": message or "Failed to create VFX graph",
    }

