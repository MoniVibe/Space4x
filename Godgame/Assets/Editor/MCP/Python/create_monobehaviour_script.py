from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="Generate a MonoBehaviour script"
)
async def create_monobehaviour_script(
    ctx: Context,
    script_name: Annotated[str, "Class name"],
    script_path: Annotated[str, "File path (e.g., 'Assets/Scripts/MyComponent.cs')"],
    namespace: Annotated[str | None, "Optional namespace"] = None,
    fields: Annotated[list[dict[str, Any]] | None, "List of field definitions"] = None,
    methods: Annotated[list[dict[str, Any]] | None, "List of method definitions"] = None,
) -> dict[str, Any]:
    await ctx.info(f"Generating MonoBehaviour script {script_name} at {script_path}")
    
    params = {
        "script_name": script_name,
        "script_path": script_path,
    }
    if namespace is not None:
        params["namespace"] = namespace
    if fields is not None:
        params["fields"] = fields
    if methods is not None:
        params["methods"] = methods
    
    response = send_command_with_retry("create_monobehaviour_script", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

