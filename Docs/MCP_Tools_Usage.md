# Unity MCP Tools Usage Guide

This document provides usage examples and reference for all custom Unity MCP tools.

## Table of Contents

1. [Instance Management](#instance-management)
2. [Component Operations](#component-operations)
3. [Prefab & Scene Operations](#prefab--scene-operations)
4. [Graph Systems](#graph-systems)
5. [ScriptableObject & MonoBehaviour](#scriptableobject--monobehaviour)
6. [DOTS & Project Tools](#dots--project-tools)

## Instance Management

### identify_unity_instance

Identify which Unity project/instance you're connected to.

**Returns:**
- `projectName` - Project folder name
- `projectType` - Inferred type (Space4x, VFXplayground, Godgame)
- `projectRoot` - Full path to project root
- `activeScene` - Current scene information
- `unityVersion` - Unity version

**Example:**
```python
result = await identify_unity_instance()
if result["data"]["projectType"] == "Space4x":
    print("Connected to Space4x project")
```

### list_unity_instances

List all available Unity instances (currently returns current instance info).

**Example:**
```python
result = await list_unity_instances()
instances = result["data"]["currentInstance"]
```

### switch_unity_instance

Switch to a specific Unity instance (requires instance identifier).

**Parameters:**
- `instance` - Instance identifier (name, hash, or name@hash)

**Example:**
```python
await switch_unity_instance(instance="Space4x@hash123")
```

## Component Operations

### add_component_to_gameobject

Add a component to a GameObject.

**Parameters:**
- `gameobject_name` - Name or instance ID
- `component_type` - Component type (short name or full namespace)
- `search_method` - "by_name" or "by_id" (default: "by_name")

**Example:**
```python
await add_component_to_gameobject(
    gameobject_name="MiningDemoSetup",
    component_type="Space4XMiningDemoAuthoring"
)
```

### configure_component_property

Set a property on a component.

**Parameters:**
- `gameobject_name` - GameObject name/ID
- `component_type` - Component type
- `property_name` - Property name (supports dot notation)
- `property_value` - Value to set

**Example:**
```python
await configure_component_property(
    gameobject_name="Carrier_1",
    component_type="Space4XCarrierAuthoring",
    property_name="speed",
    property_value=5.0
)
```

### get_component_info

Get all components and their properties on a GameObject.

**Parameters:**
- `gameobject_name` - GameObject name/ID
- `include_hidden` - Include hidden components (default: false)

**Example:**
```python
result = await get_component_info(
    gameobject_name="MiningDemoSetup",
    include_hidden=False
)
components = result["data"]["components"]
```

## Prefab & Scene Operations

### create_prefab

Create a prefab from a GameObject in the scene.

**Parameters:**
- `gameobject_name` - GameObject to convert
- `prefab_path` - Path for prefab asset
- `replace_existing` - Overwrite if exists (default: false)

**Example:**
```python
await create_prefab(
    gameobject_name="Carrier_1",
    prefab_path="Assets/Prefabs/Carriers/Carrier.prefab",
    replace_existing=True
)
```

### instantiate_prefab

Instantiate a prefab in the current scene.

**Parameters:**
- `prefab_path` - Path to prefab
- `position_x`, `position_y`, `position_z` - Position
- `parent_name` - Optional parent GameObject name

**Example:**
```python
await instantiate_prefab(
    prefab_path="Assets/Prefabs/Carriers/Carrier.prefab",
    position_x=10.0,
    position_y=0.0,
    position_z=10.0
)
```

### create_scene_with_setup

Create a new scene with template setup.

**Parameters:**
- `scene_name` - Scene name
- `scene_path` - Path for scene file
- `template` - Template type: "mining_demo", "basic", "empty", "space4x"

**Example:**
```python
await create_scene_with_setup(
    scene_name="MiningDemo",
    scene_path="Assets/Scenes/Demo/MiningDemo.unity",
    template="mining_demo"
)
```

### save_scene_as

Save current scene with new name/path.

**Parameters:**
- `scene_path` - New scene path
- `create_backup` - Create backup if exists (default: true)

**Example:**
```python
await save_scene_as(
    scene_path="Assets/Scenes/Demo/MyScene.unity",
    create_backup=True
)
```

## Graph Systems

### get_graph_structure

Read structure of a Unity graph.

**Parameters:**
- `graph_path` - Path to graph asset

**Example:**
```python
result = await get_graph_structure(
    graph_path="Assets/Shaders/MyShader.shadergraph"
)
```

### add_node_to_graph

Add a node to a graph.

**Parameters:**
- `graph_path` - Path to graph
- `node_type` - Type of node to add
- `position_x`, `position_y` - Node position
- `properties` - Node properties dict

**Example:**
```python
await add_node_to_graph(
    graph_path="Assets/Shaders/MyShader.shadergraph",
    node_type="Multiply",
    position_x=100.0,
    position_y=200.0,
    properties={"inputA": 1.0, "inputB": 2.0}
)
```

### connect_graph_nodes

Connect two nodes in a graph.

**Parameters:**
- `graph_path` - Path to graph
- `source_node_id` - Source node ID/name
- `source_port` - Output port name
- `target_node_id` - Target node ID/name
- `target_port` - Input port name

**Example:**
```python
await connect_graph_nodes(
    graph_path="Assets/Shaders/MyShader.shadergraph",
    source_node_id="Node1",
    source_port="Output",
    target_node_id="Node2",
    target_port="Input"
)
```

## ScriptableObject & MonoBehaviour

### create_scriptable_object_instance

Create a ScriptableObject asset instance.

**Parameters:**
- `script_type` - Full type name
- `asset_path` - Path for asset
- `property_values` - Property values dict

**Example:**
```python
await create_scriptable_object_instance(
    script_type="Space4X.Registry.Space4XCameraProfile",
    asset_path="Assets/Data/CameraProfile.asset",
    property_values={
        "panSpeed": 10.0,
        "zoomSpeed": 5.0,
        "zoomMinDistance": 10.0
    }
)
```

### create_monobehaviour_script

Generate a MonoBehaviour script.

**Parameters:**
- `script_name` - Class name
- `script_path` - File path
- `namespace` - Optional namespace
- `fields` - List of field definitions
- `methods` - List of method definitions

**Example:**
```python
await create_monobehaviour_script(
    script_name="MyComponent",
    script_path="Assets/Scripts/MyComponent.cs",
    namespace="MyProject",
    fields=[
        {"name": "speed", "type": "float", "serialized": True},
        {"name": "target", "type": "GameObject", "serialized": True}
    ],
    methods=[
        {
            "name": "Start",
            "returnType": "void",
            "body": "Debug.Log(\"Started\");"
        }
    ]
)
```

## DOTS & Project Tools

### query_ecs_entities

Query ECS entities by component types.

**Parameters:**
- `component_types` - List of component type names
- `with_all` - Must have all components (default: true)
- `limit` - Max entities to return (default: 100)

**Example:**
```python
result = await query_ecs_entities(
    component_types=["Carrier", "PatrolBehavior"],
    with_all=True,
    limit=50
)
```

### find_assets_by_type

Find assets by type.

**Parameters:**
- `asset_type` - Asset type name
- `search_in_subfolders` - Search recursively (default: true)

**Example:**
```python
result = await find_assets_by_type(
    asset_type="Prefab",
    search_in_subfolders=True
)
prefabs = result["data"]["assets"]
```

### get_project_info

Get project metadata.

**Returns:**
- `projectName` - Project name
- `unityVersion` - Unity version
- `graphSystems` - Available graph systems
- `packages` - Installed packages

**Example:**
```python
result = await get_project_info()
info = result["data"]
print(f"Project: {info['projectName']}")
print(f"Has Shader Graph: {info['graphSystems']['shaderGraph']}")
```

## Best Practices

1. **Always check instance first**: Use `identify_unity_instance` before operations
2. **Use full namespaces**: For component types, prefer full namespaces for reliability
3. **Handle errors**: All tools return structured responses with success/error status
4. **Save scenes**: Use `save_scene_as` after making changes
5. **Refresh assets**: Unity auto-refreshes, but explicit refresh may be needed

## Troubleshooting

- **Component not found**: Use full namespace (e.g., "Space4X.Authoring.Space4XCarrierAuthoring")
- **Graph operations fail**: Graph tools require Unity graph API access (may need enhancement)
- **ECS queries fail**: Requires World instance access (may need enhancement)
- **Prefab creation fails**: Ensure GameObject is in a scene, not already a prefab

