from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Add a component to a GameObject. Supports full namespace or short component type names."
)
async def add_component_to_gameobject(
    ctx: Context,
    gameobject_name: Annotated[str, "Name or instance ID of the GameObject"],
    component_type: Annotated[str, "Component type (e.g., 'Space4XCarrierAuthoring' or 'Space4X.Authoring.Space4XCarrierAuthoring')"],
    search_method: Annotated[str, "Search method: 'by_name', 'by_id', 'by_path'"] = "by_name",
) -> dict[str, Any]:
    await ctx.info(f"Adding component {component_type} to GameObject {gameobject_name}")
    
    params = {
        "action": "add_component",
        "gameobject_name": gameobject_name,
        "component_type": component_type,
        "search_method": search_method,
    }
    
    response = send_command_with_retry("add_component_to_gameobject", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

