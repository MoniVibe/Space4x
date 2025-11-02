Implement a Unity RTS “god-hand” camera that matches Black & White 2.

Controls (match behavior)

Rotate / Pitch: Hold Middle Mouse Button and drag to orbit around the ground point under the cursor. Limit pitch to 20°–75°. Maintain distance. 
GameFAQs
+1

Zoom: Mouse Wheel zooms in/out along the camera’s forward axis toward the cursor hit point. Clamp distance 6–120 world units. Support double-click to zoom to cursor focus. 
StrategyWiki
+1

Pan / “Grab Land”: Hold Left Mouse Button on terrain and drag to translate the camera parallel to ground (“grab land” feel). Preserve altitude. 
StrategyWiki

Snap targets: Space snaps to Temple anchor, C snaps to Creature anchor (provide two public Transforms). 
StrategyWiki

Optional keys: Arrow/WASD for pan; A/Q pitch, Z/U rotate. 
StrategyWiki

Implementation notes

Unity 2021+ on Windows. No packages required; Cinemachine optional. Single C# MonoBehaviour BW2CameraController.

Use a ground plane raycast from mouse to get the orbit/pan pivot. When rotating, orbit camera around this pivot; when zooming, move camera toward this pivot with smooth damp.

Edge cases: if raycast misses, fall back to last valid pivot. Prevent zoom clipping into terrain (raycast ahead); enforce min/max pitch and distance.

Input via Input.GetMouseButton, Input.mouseScrollDelta, Input.GetAxis("Mouse X/Y"). If using the Input System, provide an alternate wrapper.

Smoothing: critically damped spring for position and FOV/distance. Expose speeds and damp times.

Accessibility: invert Y, invert zoom scroll, sensitivity sliders.

Performance: all math in Update() using unscaled deltaTime; one Physics.Raycast per frame when needed.

Dependencies: none. No scene changes.

Public parameters
float panSpeed, rotateSpeed, pitchSpeed, zoomSpeed;
float minPitch=20f, maxPitch=75f, minDist=6f, maxDist=120f;
bool invertY, invertZoom;
Transform templeAnchor, creatureAnchor;
LayerMask groundMask;

Acceptance tests

Hold MMB and drag → camera orbits around the ground point under cursor with fixed distance and clamped pitch.

Scroll wheel → zoom toward cursor; distance stays within [minDist,maxDist]. Double-click zoom jumps to cursor focus.

Hold LMB and drag on terrain → camera translates parallel to ground (“grab” feel).

Press Space/C → instant snap to anchors with preserved pitch and a short lerp.

No terrain clipping; edge cases handled when cursor is over sky or UI.

addendum;
You are an expert Unity (Windows-only) gameplay engineer. Implement “Pivot Orbit” distance mode for our RTS/God camera to match Black & White 2-style orbiting.

Context
- Project: Unity (2021+), Windows. New Input System only.
- File to edit: Assets/Scripts/Camera/GodGameCamera.cs
- Current modes:
  - Dot Rotate: pitch/yaw in place (no translation)
  - Pivot Orbit: desired outcome (this task)

Objective
Make Pivot Orbit behave as follows:
- While MMB is held, lock a pivot point under the cursor using a hand/Mouse raycast against terrain.
- The camera rig orbits around this locked pivot on a sphere whose radius equals the current zoom distance.
- Angular sensitivity scales with distance:
  - When close to the pivot: faster yaw/pitch change (tight rotation).
  - When far from the pivot: slower yaw/pitch but larger spatial arc.
- Zoom should change radius but must not move the pivot while MMB is held.
- Keep no terrain clipping (occlusion resolution), and robust behavior when no terrain is hit.

Controls (New Input System)
- Middle Mouse Button (held): Pivot Orbit (distance mode described above)
- Scroll Wheel: zoom toward/away from the pivot without changing the pivot when MMB is held
- L key: toggle modes
  - Dot Rotate (no translation)
  - Pivot Orbit (distance-scaled spherical orbit)
- LMB: grab-land pan parallel to ground (unchanged)
- Space/C: snap to Temple/Creature anchors (unchanged)

Technical Requirements
- Pivot locking:
  - On MMB down: raycast from the camera/hand to terrain; cache pivot as Vector3 and keep it constant until MMB up.
  - If raycast misses, fallback to a horizontal plane at rig height (large max distance).
- Spherical orbit:
  - Maintain a radius r = current zoom distance.
  - Compute new rig position by applying yaw (about Vector3.up) and pitch (about tangent right axis) relative to the locked pivot, then set rig = pivot + rotatedVector.normalized * r.
  - Apply camera local pitch; rig carries yaw.
- Distance-scaled sensitivity:
  - Let s = inverseLerp(minRadius, maxRadius, distanceToPivot).
  - Angle scale = lerp(closeSensitivity, farSensitivity, sInverse). Close → higher angle delta; Far → lower.
  - Expose minRadius, maxRadius, closeSensitivity, farSensitivity in Inspector.
- Zoom behavior in Pivot Orbit:
  - When MMB is held, mouse wheel only adjusts radius r (zoom distance) and recomputes rig = pivot + direction * r. Do not move pivot.
- Occlusion / collision:
  - Use spherecast from pivot to camera to avoid clipping and pull camera forward by a small clearance if needed.
- Ground adaptation:
  - Do NOT ground-lock height while in Pivot Orbit (to preserve spherical motion).
- New Input System only:
  - No Legacy Input calls. Use Keyboard.current / Mouse.current or injected input provider consistent with the file.

Inspector Tunables (serialize, with tooltips)
- orbitMinRadius (default ~2f), orbitMaxRadius (default ~100f)
- closeAngleSensitivity (default ~3.0)
- farAngleSensitivity (default ~0.3)
- occluderLayers, cameraCollisionRadius, occlusionClearance
- invertZoom, invertMouseRotation
- orbitMode toggle key (default L)

Edge Cases
- If UI blocks pointer or ray misses terrain: fallback plane at rig height; keep last valid pivot if already locked.
- If pivot or computed position becomes invalid (NaN/Inf), recover to last safe rig position and log a clear error.
- Respect world boundaries if enabled.

Non-Functional Constraints
- Keep interfaces stable. No package/version changes.
- Windows only; no platform-specific APIs.
- Fail fast with actionable logs if configuration is invalid (e.g., ground layer missing).
- Do not add unspecced features.

Deliverables
- Updated GodGameCamera.cs implementing the above.
- Clear, concise serialized fields with headers and tooltips.
- Minimal inline logs for mode toggles and recoveries.
- No Legacy Input usage.

Acceptance Tests
- Hold MMB near the hand raycast point: camera rotates quickly with a tight spherical path around the locked pivot.
- Hold MMB far from the pivot: camera rotates more slowly but moves along a larger arc; pivot remains fixed.
- While MMB is held, scrolling changes radius but never moves the pivot.
- Releasing MMB unlocks the pivot; re-pressing MMB locks the new hand raycast point.
- No terrain clipping due to occlusion solving.
- L toggles between Dot Rotate and Pivot Orbit; console logs reflect the current mode.

Definition of Done
- Behavior matches the above tests consistently.
- New Input System only.
- Compiles cleanly with no linter errors or regressions in Dot Rotate, pan, zoom, or anchor snaps.

Implement LMB “grab-land” panning so the cursor drags the terrain itself. No fixed speed. The world point under the cursor should stay under the cursor while dragging.

Behavior

On LMB down: raycast from camera through mouse to terrain. If hit, create a grab plane:

Origin = hit point G0.

Normal = terrain normal at G0 (fallback Vector3.up).

Store lastMouseWorld = RayIntersect(grabPlane, mousePos).

While LMB held each frame:

mouseWorld = RayIntersect(grabPlane, mousePos).

delta = mouseWorld - lastMouseWorld.

Translate the rig by -delta (pure world translation, no rotation).

lastMouseWorld = mouseWorld.

On LMB up: clear grab state.

Notes

This yields absolute mapping. No pan speed scalar. The translation is derived from geometry, not time.

Preserve altitude: after translation, keep camera’s height above terrain equal to its pre-grab value (sample terrainHeight at camera XZ; add stored offset).

If the first raycast misses, build the plane through G0 using world up at the camera’s current ground-projected point. While held, never update the plane origin or normal.

UI hover: ignore panning when pointer over UI.

Do not parent the camera under scaled transforms. Do math in world space.

Disable any Cinemachine damping during evaluation.

Core code (replace any velocity-based pan)
// fields
bool grabbing;
Plane grabPlane;
Vector3 lastMouseWorld;
float heightOffset; // camera.y - terrainHeightAt(camera.xz)

void BeginGrab(Vector2 mousePos) {
    if (TryTerrainHit(mousePos, out RaycastHit hit)) {
        grabPlane = new Plane(hit.normal.sqrMagnitude > 0.1f ? hit.normal : Vector3.up, hit.point);
    } else {
        // fallback plane through ground-projected camera point
        Vector3 p = new Vector3(cam.position.x, SampleTerrain(cam.position), cam.position.z);
        grabPlane = new Plane(Vector3.up, p);
    }
    lastMouseWorld = RayOnPlane(mousePos, grabPlane);
    heightOffset = cam.position.y - SampleTerrain(cam.position);
    grabbing = true;
}

void UpdateGrab(Vector2 mousePos) {
    Vector3 mouseWorld = RayOnPlane(mousePos, grabPlane);
    Vector3 delta = mouseWorld - lastMouseWorld;
    camRig.position -= delta;           // move world opposite to cursor drag
    lastMouseWorld = mouseWorld;

    // preserve altitude
    Vector3 p = cam.position;
    float ground = SampleTerrain(p);
    cam.position = new Vector3(p.x, ground + heightOffset, p.z);
}

static Vector3 RayOnPlane(Vector2 mousePos, Plane plane) {
    Ray r = cam.ScreenPointToRay(mousePos);
    plane.Raycast(r, out float t);
    return r.GetPoint(t);
}

Diagnostics

Assert the grabbed point remains under cursor:

// After UpdateGrab
Vector3 grabbed = grabPlane.ClosestPointOnPlane(lastMouseWorld);
Vector2 sp = cam.WorldToScreenPoint(grabbed);
if ((sp - (Vector2)Input.mousePosition).sqrMagnitude > 1f) Debug.LogError("Grab drift");


Log that no pan “speed” is used and that delta magnitude tracks mouse movement distance on the plane.

Acceptance tests

With LMB held, drag slowly and quickly. The same ground feature sticks to the cursor with no acceleration feel.

On slopes, dragging uphill or downhill preserves camera altitude offset and shows no sideways slippage.

Switching to MMB orbit keeps the previously grabbed point unchanged unless MMB sets a new pivot.