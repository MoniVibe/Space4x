# GodGame — READ BEFORE WORKING

Authoritative orientation for all contributors. Follow this to avoid regressions, slow compiles, and assembly chaos.

---

## 0) TruthSource

All project rules live under `Assets/TruthSource/`.

* Start with: `Assets/TruthSource/Project/Orientation.md` (this file) and task‑specific briefs inside `Assets/TruthSource/<Area>/...`.
* When in doubt, read TruthSource before touching code or config.

---

## 1) Assembly Policy (Hard Rule)

Exactly **six** assemblies. No exceptions.

**Runtime**

1. `GodGame.Core`
2. `GodGame.World` → depends on Core
3. `GodGame.Interaction` → depends on Core, World, `Unity.InputSystem`
4. `GodGame.Miracles` → depends on Core, World, Interaction
5. `GodGame.Villagers` → depends on Core, World

**Editor**
6. `GodGame.Editor` (Editor‑only) → depends on Core, World, Interaction, Miracles, Villagers

Rules:

* No other runtime asmdefs. No nested runtime asmdefs under these roots.
* Delete all `.asmref` under `Assets/Scripts/**` unless explicitly required to merge into an existing external assembly.
* **No cycles.** Runtime assemblies never reference `UnityEditor` or `GodGame.Editor`.
* Only `GodGame.Interaction` references `Unity.InputSystem`.

---

## 2) Folder Ownership → Assembly Map

* `Assets/Scripts/Core`, `Assets/Scripts/Framework`, `Assets/Scripts/Utility` → **Core**
* `Assets/Scripts/World`, `Assets/Scripts/Physics`, `Assets/Scripts/Resources`, `Assets/Scripts/Time`, `Assets/Scripts/Vegetation` → **World**
* `Assets/Scripts/Interaction`, `Assets/Scripts/Input`, `Assets/Scripts/Camera`, `Assets/Scripts/Hand`, `Assets/Scripts/Slingshot` → **Interaction**
* `Assets/Scripts/Miracles/**` → **Miracles**
* `Assets/Scripts/Villagers`, `Assets/Scripts/AI` → **Villagers**
* `Assets/Editor/**` and any `*/Editor/**` → **Editor** (Editor‑only asmdefs allowed here)

Do not create new top‑level script buckets without approval. If a feature spans areas, split its parts by these ownership rules.

---

## 3) Namespace Rules

* One root per assembly: `GodGame.Core`, `GodGame.World`, `GodGame.Interaction`, `GodGame.Miracles`, `GodGame.Villagers`, `GodGame.Editor`.
* Subfolders become sub‑namespaces, e.g., `GodGame.World.Resources`.
* Time contracts live in `GodGame.World.Time` (e.g., `ITimeAware`, `TimeHistory`, `KeyframeStore`, `TimeEngine`, `TimeSettings`). Call sites must import `GodGame.World.Time` explicitly.
* Match namespace to folder on move or add. No cross‑area namespaces.

---

## 4) Input System

* Active Input Handling: **New Input System**.
* Only **Interaction** uses `UnityEngine.InputSystem` and references `Unity.InputSystem` asmdef. No other runtime assembly may add this reference.
* If another assembly needs input, refactor the call through Interaction instead of adding more references.

---

## 5) Editor vs Runtime

* Runtime code must compile for player builds. No `UnityEditor` usage there.
* Place tools, menus, and importers in `Assets/Editor/**` under `GodGame.Editor`.
* If runtime code needs editor hooks, use `#if UNITY_EDITOR` guards in partial classes kept inside Editor.

---

## 6) Dependencies

* Direction is **Core → World → Interaction → Miracles**. Villagers peers with Miracles but depends only on Core, World.
* World must **not** reference Interaction. If World needs to talk to a Pickable:
  * Option A (preferred): define a minimal `GodGame.IPickable` interface in Core; have Interaction’s `PickableObject` implement it; World only depends on the interface.
  * Option B: move the pickable component onto Interaction and keep World prefabs free of Interaction types.
* Extract shared types to **Core** to break cycles.
* Do not depend upward or sideways without an approved design note in TruthSource.

---

## 7) Adding a Feature (Standard Flow)

1. Read the relevant brief in `Assets/TruthSource/<Area>/`.
2. Choose target assembly per §2. Create files in that folder with correct namespace per §3.
3. If you need symbols from another assembly, confirm the dependency exists in §1. If not allowed, add an adapter in the lower layer or move interfaces to Core.
4. Keep public surface minimal. Prefer internal where possible.
5. Add unit tests under `Assets/Tests/<Area>/` with **Test Assemblies** checked and referencing only needed runtime assemblies.
6. Update `Assets/TruthSource/<Area>/CHANGELOG.md`.

---

## 8) Common Pitfalls (Do Not Do)

* Creating new asmdefs or `.asmref` inside `Assets/Scripts/**`.
* Referencing `UnityEditor` from runtime assemblies.
* Making Interaction depend on Miracles or Villagers.
* Re‑introducing a custom `GodGame.Time.*` API. Use `UnityEngine.Time`.
* Committing `Library/` or auto‑generated sln/csproj files.

---

## 9) Migration and Fix Strategy

* If namespace errors appear, first verify the six asmdefs and their references match §1.
* Remove stray asmdefs/asmrefs, then reimport. If still broken, delete `Library/` and reopen.
* Replace any `GodGame.Time.*` usages with `UnityEngine.Time.*`.
* Add `using GodGame.World.Time` for time contracts and `using GodGame.World.{Resources|Physics|Systems}` (or fully qualify types from World).

---

## 10) PR Checklist

* [ ] Files in correct folders and namespaces.
* [ ] No new asmdefs/asmrefs added under `Assets/Scripts/**`.
* [ ] No new assembly dependency beyond §1 rules.
* [ ] Tests pass locally. Editor scripts guarded or in Editor assembly.
* [ ] TruthSource doc updated for any new behavior.

---

## 11) Local Build Hygiene

* `Assets → Reimport All` after assembly graph edits.
* Clean build: close Unity and delete `Library/` if resolution errors persist.
* Keep `Player Settings → Scripting Define Symbols` aligned with TruthSource.

---

## 12) Tools

* Menu: `Tools/God Game/Fix Project Errors` validates packages and asmdefs.
* `Tools/God Game/Logs/Errors Only` shows filtered compiler errors.
* Editor script that writes the six asmdefs: `Assets/Editor/AsmdefPlan.cs`.

---

## 12a) Task Intake Checklist for Agents

Before starting any task:

1. Read the relevant brief under `Assets/TruthSource/<Area>/...` and `Assets/TruthSource/Project/Orientation.md`.
2. Confirm your target assembly per §2 and that your file’s namespace matches its folder per §3 (use `GodGame.World.Time` for time contracts).
3. Verify the assembly graph per §1 and §6 (no World→Interaction, only Interaction references `Unity.InputSystem`).
4. After edits to asmdefs or folder ownership, run `Assets → Reimport All`. If resolution errors persist, close Unity and delete `Library/`.

---

## 13) Escalation

If a change requires a new dependency direction or assembly, write a one‑page design in `Assets/TruthSource/Design/` and get approval before coding.

---

## 14) Contacts

* Owners: Core/World/Interaction/Miracles/Villagers/Editor leads listed in `Assets/TruthSource/Project/OWNERS.md`.

You are responsible for reading TruthSource first. Deviations without approval will be rejected.
