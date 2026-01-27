# Content Neutrality Linting Guidelines

## Overview

PureDOTS core systems must remain theme-neutral and reusable across multiple games. This document outlines linting rules and enforcement strategies to ensure naming conventions and design patterns maintain neutrality.

## Linting Rules

### Rule 1: Theme-Specific Terms in PureDOTS.Runtime

**Violation**: Components, systems, or types in `PureDOTS.Runtime.*` namespaces contain game-specific terms.

**Examples**:
- ❌ `VillagerJobSystem` → ✅ `WorkerJobSystem`
- ❌ `TreeGrowthSystem` → ✅ `GrowthNodeSystem`
- ❌ `StorehouseInventoryComponent` → ✅ `StorageInventoryComponent`

**Enforcement**: Roslyn analyzer scans `PureDOTS.Runtime.*` namespaces and flags violations.

### Rule 2: Domain-Specific Types in Shared Systems

**Violation**: Shared systems reference game-specific component types directly.

**Example**:
```csharp
// ❌ Bad: Direct reference to game-specific type
if (entityManager.HasComponent<Godgame.Villager>(entity)) { }

// ✅ Good: Use marker components or registries
if (entityManager.HasComponent<VillagerId>(entity)) { }
```

**Enforcement**: Static analysis flags direct references to game assembly types in shared systems.

### Rule 3: Namespace Boundaries

**Violation**: Game assemblies modify or depend on internals of `PureDOTS.Runtime.*`.

**Enforcement**: Assembly references checked to ensure games only reference public APIs.

## CI Integration

### Roslyn Analyzer (Future)

Create a custom Roslyn analyzer that:

1. Scans `PureDOTS.Runtime.*` namespaces for theme-specific terms
2. Flags components that reference game-specific types
3. Validates namespace boundaries

**Implementation Plan**:
- Create analyzer project (`PureDOTS.Tools.NeutralityAnalyzer`)
- Define diagnostic rules for theme-specific terms
- Integrate into CI pipeline as pre-commit hook or PR check

### CI Lint Script (Interim)

Until Roslyn analyzer is ready, use a simple script-based approach:

```bash
# Check for theme-specific terms in PureDOTS.Runtime
grep -r "Villager\|Tree\|Storehouse" PureDOTS/Packages/com.moni.puredots/Runtime/Runtime --exclude-dir=.*
```

**CI Integration**: Add to `.github/workflows/lint.yml` or equivalent CI config.

## Testing Neutrality

### Integration Tests

Add tests that verify:

1. Shared systems work with mock/test entities (not game-specific types)
2. Games can provide test fixtures using `RegistryMocks`
3. No direct dependencies on game assemblies in shared code

**Example**:
```csharp
[Test]
public void SharedSystem_WorksWithMockEntities()
{
    // Create mock entities using neutral components
    var mockResource = RegistryMocks.CreateMockResource(...);
    
    // Verify shared system processes mock correctly
    // No game-specific types referenced
}
```

## Manual Review Checklist

When reviewing PRs that touch `PureDOTS.Runtime.*`:

- [ ] No theme-specific naming (villager, tree, storehouse, etc.)
- [ ] No direct references to game assembly types
- [ ] Components use generic names (Worker, ResourceNode, StorageBuilding)
- [ ] Systems query via registries or marker components, not game types
- [ ] Configuration is data-driven (blobs, ScriptableObjects)

## References

- `Docs/DesignNotes/SystemIntegration.md` - Integration contracts and neutrality guidelines
- `Docs/TODO/SystemIntegration_TODO.md` - Integration task tracking


