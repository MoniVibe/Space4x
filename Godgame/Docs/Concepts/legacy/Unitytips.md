Critical Unity Time-Savers & Hidden Gotchas
🔴 Package Manager Issues

Input System Hell: When you install Input System package, Unity asks "Do you want to enable the new Input System?" - if you click Yes, it DISABLES the old Input Manager completely. Many assets break.

Fix: Go to Edit → Project Settings → Player → Active Input Handling = "Both"


Package conflicts: Some packages silently conflict. Never install both Cinemachine and your own camera system without expecting issues.
Missing dependencies: Package Manager sometimes doesn't auto-install dependencies. Check the console after installing anything.

🔴 Assembly Definition Gotchas

Test assemblies: Never reference test assemblies from runtime code
Platform-specific: Editor assemblies MUST have "includePlatforms": ["Editor"] or they'll try to build for player
.asmdef location: Must be in the root of the folder it's meant to cover, not in subfolders
Newtonsoft.Json: If you need JSON, Unity has com.unity.nuget.newtonsoft-json package - don't import your own

🔴 Script Compilation Order

[DefaultExecutionOrder]: This attribute is your friend for timing issues
Execution order settings: Edit → Project Settings → Script Execution Order (many don't know this exists)
Awake vs Start: Awake() runs even on disabled objects, Start() doesn't
Script reload: Domain Reload on play is slow but turning it off breaks static variables

🔴 Unity "Features" That Waste Hours

Prefab variants: Modifying a prefab variant can break in non-obvious ways. Always check "Overrides" dropdown
Scene loading: DontDestroyOnLoad objects duplicate if you reload the same scene
Transform.position vs localPosition: In UI, always use RectTransform.anchoredPosition, not position
Layer masks: 1 << layerNumber not just layerNumber
Tags/Layers: Are project-wide settings stored in ProjectSettings, not per-scene

🔴 Performance Traps

FindObjectOfType in Update(): NEVER. Cache it.
GameObject.Find(): Death by string comparison. Use tags or cache references.
SendMessage(): 2000x slower than direct calls
Instantiate/Destroy: Always pool if happening frequently
Debug.Log in builds: Still executes string formatting even if not displayed

🔴 Build & Platform Issues

Resources folder: Includes EVERYTHING in builds, even unused. Use Addressables instead.
StreamingAssets: Read-only on mobile, don't try to write
File paths: Use Path.Combine(), never manual "/" or "\"
Platform dependent compilation: Use #if UNITY_EDITOR not if (Application.isEditor)

🔴 Common Setup Issues

Git/Version Control:

MUST use "Visible Meta Files" in Editor Settings
Force Text serialization, not binary
.gitignore must exclude Library/, Temp/, Logs/, UserSettings/


Lighting: "Auto Generate" lighting will kill performance in editor. Turn it off.
Quality Settings: You might be testing on "Ultra" while build defaults to "Low"

🔴 Inspector/Serialization Traps

[SerializeField] private: Shows in inspector but survives refactoring better than public
ScriptableObject changes: Don't apply in play mode - they persist!
Prefab modifications: Apply to prefab affects ALL instances across ALL scenes
Hidden components: Disabled components still run Awake()
List/Array resize: Changing size in inspector during play loses data

🔴 Navigation/Physics

NavMesh: Needs rebaking when geometry changes
Rigidbody sleep: Won't detect collisions when sleeping
Collider bounds: Mesh colliders on scaled objects = wrong bounds
Layer collision matrix: Physics settings, not per-object

🔴 Quick Diagnostic Commands
When things break mysteriously:

Reimport All (right-click Assets folder)
Clear Console then check for warnings you've been ignoring
Delete Library folder (Unity rebuilds it)
Check .meta files aren't corrupted (especially after Git merges)
Window → Analysis → Profiler (check for spikes)
Window → Analysis → Console (check collapse toggle - might be hiding 1000s of errors)

🔴 The "Why isn't this working?" Checklist

Is the GameObject active?
Is the component enabled?
Is it on the right layer?
Did you save the scene?
Are you modifying a prefab or instance?
Is Time.timeScale = 0?
Did you forget to assign in inspector?
Is it destroyed before it runs?
Are you in Play Mode?
Did scripts actually recompile? (spinner in bottom right)

🔴 Your Current Project Specifically
Based on your errors:

You have MCP bridge which is good but it can timeout - restart it if it stops responding
You're mixing old patterns (Resources folder) with new (assembly definitions) - pick one era
Multiple "GodGame.Tools.asmdef" = someone duplicated folders without cleaning up
Unity 2022+ changed many APIs - tutorials from 2019 won't work verbatim