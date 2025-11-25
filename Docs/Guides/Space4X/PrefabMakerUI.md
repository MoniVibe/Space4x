# Space4X Prefab Maker UI Guide

## Overview

The Space4X Prefab Maker provides a comprehensive editor interface for creating and managing prefabs from catalog data. The tool is catalog-driven, meaning it reads from `*CatalogAuthoring` prefabs and generates Unity prefabs with appropriate authoring components.

## Opening the Window

Access the Prefab Maker via `Tools → Space4X → Prefab Maker` in the Unity menu.

## Editor Tab

The Editor tab provides a split-pane interface for browsing and editing templates.

### Left Panel: Template List

- **Category Tabs**: Switch between Hulls, Modules, Stations, Resources, Products, Aggregates, FX, and Individuals
- **Search Filter**: Type to filter templates by ID or display name
- **Template List**: Click a template to select it for editing
- **Validation Badge**: Templates with validation issues show a "!" indicator

### Right Panel: Template Editor

When a template is selected, the editor panel shows:

- **Common Fields**: ID, display name, description, style tokens (palette, roughness, pattern)
- **Category-Specific Fields**: Fields relevant to the selected template type
- **Derived Properties**: Read-only computed values (e.g., socket counts, mount summaries)
- **Validation Issues**: Any validation problems displayed at the bottom

### Category-Specific Editors

#### Hull Editor
- Base mass, field refit allowed, category, hangar capacity
- Socket management (add/remove sockets by type and size)
- Built-in module loadouts
- Variant selection (Common, Uncommon, Heroic, Prototype)

#### Module Editor
- Module class, mount requirements (type and size)
- Stats: mass, power draw, offense/defense/utility ratings, efficiency
- Function metadata (if function is not None)
- Quality/rarity/tier/manufacturer attributes
- Facility archetype and tier (for facility modules)

#### Station Editor
- Refit facility flag and zone radius
- Presentation archetype

#### Resource/Product/Effect Editors
- Presentation archetype
- Product-specific: required tech tier

#### Aggregate Editor
- Composed profile IDs (template, outlook, alignment, personality, theme)
- Policy field overrides (optional)

#### Individual Editor
- Role and progression track
- Core stats (Command, Tactics, Logistics, Diplomacy, Engineering, Resolve)
- Physique/Finesse/Will with inclinations
- Alignment (Law, Good, Integrity)
- Race/Culture IDs
- Contract information
- Relations (titles, loyalty, patronage, etc.) - managed at runtime

### Actions

- **Generate Selected Prefab**: Generate prefab for the currently selected template
- **Validate Selected**: Run validation on the selected template and show issues
- **Generate All Prefabs**: Generate all prefabs from catalogs (same as Batch Generate tab)

## Batch Generate Tab

Generate all prefabs from catalogs with options:

- **Catalog Path**: Path to catalog prefabs (default: `Assets/Data/Catalogs`)
- **Placeholders Only**: Generate placeholder visuals (primitive meshes)
- **Overwrite Missing Sockets**: Regenerate sockets even if prefab exists
- **Dry Run**: Preview changes without writing files
- **Category Filters**: Toggle which categories to generate

## Adopt/Repair Tab

Scan and repair existing prefabs:

- **Scan and Repair**: Find and fix issues in existing prefabs
- **Bulk Edit**: Filter prefabs by ID/path and apply fixes
- **Fix All Invalid Fasteners**: Fix ID policy violations
- **Normalize All IDs**: Normalize ID casing

## Validate Tab

Run comprehensive validation on all prefabs:

- **Run Validation**: Check all prefabs against catalog data
- **Filtered View**: Filter issues by type (errors, warnings, missing sockets, tier mismatches, orphaned prefabs)
- **Grouped Issues**: Issues grouped by category for easier navigation
- **Quick Actions**: Select prefabs with issues, export report to JSON

## Profiles Tab

Build aggregate profile combo tables:

- **Build Combo Table**: Generate all valid combinations of template/outlook/alignment profiles
- **Materialize Token Prefabs**: Create prefabs for selected aggregate combos

## Preview Tab

Preview generation without writing files:

- **Generate Preview**: Run generation in dry-run mode
- **Export Screenshot + JSON Diff**: Export preview data for comparison

## Workflow Examples

### Creating a New Hull Prefab

1. Open Prefab Maker → Editor tab
2. Select "Hulls" category tab
3. Templates are loaded from `HullCatalog.prefab`
4. Select a hull template from the list
5. Edit properties in the right panel (sockets, hangar capacity, etc.)
6. Click "Generate Selected Prefab"
7. Prefab is created at `Assets/Prefabs/Space4X/{Category}/{id}.prefab`

### Editing Module Properties

1. Select "Modules" category tab
2. Search for the module you want to edit
3. Select it from the list
4. Modify quality, rarity, tier, or manufacturer in the editor
5. Click "Validate Selected" to check for issues
6. Click "Generate Selected Prefab" to update the prefab

### Batch Generation

1. Go to Batch Generate tab
2. Set catalog path
3. Configure options (placeholders, overwrite sockets, dry run)
4. Select category filters
5. Click "Generate All Prefabs"
6. Check console for results (created/updated/skipped counts)

### Validation Workflow

1. Go to Validate tab
2. Click "Run Validation"
3. Review grouped issues (missing sockets, tier mismatches, etc.)
4. Use "Select" buttons to jump to problematic prefabs
5. Fix issues in the Editor tab or directly in prefabs
6. Re-run validation to confirm fixes

## Tips

- **Catalog-Driven**: Always edit catalog prefabs first, then regenerate prefabs. The Editor tab shows catalog data but doesn't modify catalogs directly.
- **Dry Run First**: Use dry-run mode to preview changes before writing files
- **Validation**: Run validation regularly to catch issues early
- **Targeted Generation**: Use "Generate Selected Prefab" for quick iteration on a single prefab
- **Search**: Use the search filter to quickly find templates in large catalogs

## Troubleshooting

### Templates Not Loading
- Check that catalog path is correct
- Verify catalog prefabs exist at the specified path
- Check console for loading errors

### Generation Fails
- Check validation issues first
- Ensure catalog data is valid (non-empty IDs, valid enums)
- Check console for detailed error messages

### Prefabs Not Updating
- Ensure "Overwrite Missing Sockets" is enabled if regenerating sockets
- Check that prefab paths match catalog IDs exactly
- Verify prefab directories exist

