from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Configure a property on a component. Supports nested properties using dot notation (e.g., 'resourceStorages.0.capacity')."
)
async def configure_component_property(
    ctx: Context,
    gameobject_name: Annotated[str, "Name or instance ID of the GameObject"],
    component_type: Annotated[str, "Component type name"],
    property_name: Annotated[str, "Property name (supports dot notation for nested properties)"],
    property_value: Annotated[Any, "Value to set (will be converted to appropriate type)"],
    search_method: Annotated[str, "Search method: 'by_name', 'by_id'"] = "by_name",
) -> dict[str, Any]:
    await ctx.info(f"Setting {component_type}.{property_name} = {property_value} on {gameobject_name}")
    
    params = {
        "action": "configure_property",
        "gameobject_name": gameobject_name,
        "component_type": component_type,
        "property_name": property_name,
        "property_value": property_value,
        "search_method": search_method,
    }
    
    response = send_command_with_retry("configure_component_property", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

