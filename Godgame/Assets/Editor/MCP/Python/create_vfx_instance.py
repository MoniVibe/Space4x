from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Create a VFX instance in the scene with specified graph and parameters"
)
async def create_vfx_instance(
    ctx: Context,
    template_id: Annotated[str, "Graph ID (filename without extension)"] = None,
    graph_id: Annotated[str, "Graph ID (alias for template_id)"] = None,
    graph_path: Annotated[str, "Path to the VFX graph asset"] = None,
    transform: Annotated[dict[str, Any], "Transform (position, rotation, scale, parent)"] = None,
    params: Annotated[dict[str, Any], "Parameter values to apply"] = None,
    instance_name: Annotated[str, "Name for the GameObject instance"] = "VFXInstance",
) -> dict[str, Any]:
    if not graph_path and not template_id and not graph_id:
        return {"success": False, "message": "Either graph_path or template_id/graph_id is required"}
    
    await ctx.info(f"Creating VFX instance: {graph_path or template_id or graph_id}")
    
    request_params = {}
    if graph_path:
        request_params["graph_path"] = graph_path
    if template_id:
        request_params["template_id"] = template_id
    if graph_id:
        request_params["graph_id"] = graph_id
    if transform:
        request_params["transform"] = transform
    if params:
        request_params["params"] = params
    if instance_name:
        request_params["instance_name"] = instance_name
    
    response = send_command_with_retry("create_vfx_instance", request_params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

