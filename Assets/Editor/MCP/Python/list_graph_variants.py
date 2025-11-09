from typing import Annotated, Any
from fastmcp import Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

@mcp_for_unity_tool(
    description="List available VFX graph variants (contexts, operators, parameters) for discovery"
)
async def list_graph_variants(
    ctx: Context,
    category: Annotated[str | None, "Filter by category (Context, Operator, Parameter)"] = None,
    search: Annotated[str | None, "Search term to filter variants by name or identifier"] = None,
    limit: Annotated[int | None, "Maximum number of variants to return (default: 500)"] = None,
) -> dict[str, Any]:
    await ctx.info(f"Listing VFX graph variants (category={category}, search={search}, limit={limit})")
    
    params = {}
    if category is not None:
        params["category"] = category
    if search is not None:
        params["search"] = search
    if limit is not None:
        params["limit"] = limit
    
    response = send_command_with_retry("list_graph_variants", params)
    return response if isinstance(response, dict) else {"success": False, "message": str(response)}

