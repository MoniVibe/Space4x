# Prefab Maker: Godgame Pattern Adoption

## Overview

After reviewing Godgame's prefab tool implementation, we've adopted several patterns that make the Space4X Prefab Maker more comfortable to work with.

## Key Improvements Adopted from Godgame

### 1. PlaceholderPrefabUtility Pattern ✅

**Godgame Pattern:**
- Clean utility class `PlaceholderPrefabUtility` for creating placeholder visuals
- Visuals created as child GameObjects named "Visual"
- Primitive meshes with appropriate scales per prefab type

**Space4X Implementation:**
- Created `Assets/Editor/Space4X/PlaceholderPrefabUtility.cs`
- Visuals are now child GameObjects (not on root)
- Type-based primitive selection (Hull=Capsule, Module=Cube, Station=Cylinder, FX=Quad)
- Automatic collider removal (not needed for ECS)
- Default material assignment

**Benefits:**
- Cleaner prefab hierarchy
- Easier to swap visuals later
- Consistent with Godgame patterns

### 2. Visual as Child GameObject ✅

**Before:**
```csharp
// Visual components directly on root GameObject
var filter = obj.AddComponent<MeshFilter>();
var renderer = obj.AddComponent<MeshRenderer>();
```

**After (Godgame Pattern):**
```csharp
// Visual as child GameObject
var visual = GameObject.CreatePrimitive(primitiveType);
visual.name = "Visual";
visual.transform.SetParent(parent.transform);
```

**Benefits:**
- Cleaner separation of data (root) vs. presentation (child)
- Easier to swap/replace visuals without touching authoring components
- Matches Godgame's comfortable workflow

### 3. Cleaner Authoring Component Structure

**Godgame Pattern:**
- Simple, focused authoring components
- Minimal serialized fields
- Clean bakers that just add components/buffers

**Space4X Alignment:**
- `HullIdAuthoring` - Simple ID component
- `ModuleIdAuthoring` - Simple ID component  
- `MountRequirementAuthoring` - Simple mount type/size
- `HullSocketAuthoring` - Marks hull as having sockets (sockets created by Prefab Maker)

**Benefits:**
- Components are easy to understand
- Clear separation: authoring components = data, Prefab Maker = structure

### 4. Folder Structure Consistency

**Godgame Pattern:**
```
Assets/Prefabs/
├── Buildings/
├── Individuals/
├── Equipment/
└── ...
```

**Space4X Pattern:**
```
Assets/Prefabs/Space4X/
├── Hulls/
├── Modules/
├── Stations/
└── FX/
```

**Benefits:**
- Consistent organization
- Easy to find prefabs by category
- Matches team expectations

### 5. Prefab Generation Separation

**Godgame Pattern:**
- `PrefabGenerator` - Core generation logic
- `PlaceholderPrefabUtility` - Visual creation
- `PrefabEditorWindow` - UI

**Space4X Pattern:**
- `PrefabMaker` - Core generation logic
- `PlaceholderPrefabUtility` - Visual creation (NEW)
- `PrefabMakerWindow` - UI

**Benefits:**
- Clear separation of concerns
- Easier to test and maintain
- Matches Godgame's comfortable architecture

## Patterns We Kept Different (Space4X-Specific)

### Catalog-Driven Generation
- **Godgame:** Template-based (ScriptableObject/JSON templates)
- **Space4X:** Catalog-driven (reads from `HullCatalogAuthoring`/`ModuleCatalogAuthoring`)
- **Reason:** Space4X uses catalog blobs as source of truth; templates would be redundant

### Socket Generation
- **Godgame:** Uses `ModuleSlotIds.AddWagonSlots()` static helpers
- **Space4X:** Generates sockets from catalog slot data
- **Reason:** Sockets come from hull catalog definitions, not hardcoded lists

### Binding System
- **Godgame:** Presentation binding handled separately
- **Space4X:** Generates binding JSON/blob from prefabs
- **Reason:** Space4X needs ID → prefab mapping for runtime spawning

## Usage Comparison

### Godgame Workflow
1. Open `Godgame → Prefab Editor`
2. Create/edit templates
3. Generate prefabs
4. Visuals auto-created as child GameObjects

### Space4X Workflow (After Improvements)
1. Open `Tools/Space4X/Prefab Maker`
2. Select catalog path
3. Click "Generate All Prefabs"
4. Visuals auto-created as child GameObjects (NEW)

## Next Steps (Optional Future Improvements)

1. **ScriptableObject Templates** (like Godgame)
   - Could add template system for stations/FX that aren't in catalogs yet
   - Would make it easier to create variants

2. **Validation Panel UI** (like Godgame Phase 3)
   - Rich diagnostics showing "why invalid"
   - Substitution suggestions
   - Rule-based validation

3. **Dry-Run Export** (like Godgame)
   - JSON export of what would change
   - Diff summary
   - Better CI integration

## Files Modified

- ✅ `Assets/Editor/Space4X/PlaceholderPrefabUtility.cs` - NEW (Godgame pattern)
- ✅ `Assets/Editor/Space4X/PrefabMaker.cs` - Updated to use PlaceholderPrefabUtility
- ✅ Visual creation now uses child GameObjects

## Conclusion

Adopting Godgame's patterns makes the Space4X Prefab Maker:
- **More comfortable** - Visuals as children, cleaner structure
- **More maintainable** - Clear separation of concerns
- **More consistent** - Matches team's existing patterns

The core functionality remains Space4X-specific (catalog-driven), but the presentation and structure now follow Godgame's proven patterns.

## UI Overhaul (2024 Update)

The Prefab Maker now includes a comprehensive editor UI inspired by Godgame's `PrefabEditorWindow`:

### New Editor Tab
- **Split-pane layout**: Left panel shows filtered template list, right panel shows detailed editor
- **Category tabs**: Hulls, Modules, Stations, Resources, Products, Aggregates, FX, Individuals
- **Search filtering**: Filter templates by ID or display name
- **Template editing**: Dedicated editor panels for each category with all relevant fields
- **Targeted generation**: Generate individual prefabs or selected categories
- **Inline validation**: Validation issues displayed directly in the editor

### Template Model Layer
- **Editor-only templates**: `PrefabTemplate` base class with category-specific subclasses
- **Catalog bridge**: `CatalogTemplateBridge` loads catalog data into template models
- **Derived properties**: Templates compute summary strings and socket counts for UI display
- **Serialization**: JSON export/import for diff-friendly bulk editing

### Targeted Generation
- **Selected IDs**: Generate prefabs for specific template IDs
- **Category filtering**: Generate only a specific category (Hulls, Modules, etc.)
- **Integration**: All generators respect `SelectedIds` and `SelectedCategory` filters

### Validation Enhancements
- **Template validation**: `TemplateValidator` validates templates before generation
- **Issue tracking**: Templates track validation issues and validity state
- **Quick fixes**: Validation issues displayed with context in the editor

