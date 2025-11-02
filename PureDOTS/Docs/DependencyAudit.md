# Package & Dependency Audit

The DOTS template currently references the following key packages:

## Core DOTS Stack (required)
- `com.unity.entities` `1.4.2`
- `com.unity.entities.graphics` `1.4.15`
- `com.unity.collections` `2.6.2`
- `com.unity.jobs` `0.70.0-preview.7`
- `com.unity.physics` `1.0.16`
- `com.unity.mathematics` `1.3.2`
- `com.unity.burst` `1.8.24`

## Rendering/Input (recommended)
- `com.unity.render-pipelines.universal` `17.2.0`
- `com.unity.inputsystem` `1.7.0`
- `com.unity.textmeshpro` `5.0.0`

## Networking/Multiplayer (optional)
- `com.unity.netcode` `1.8.0`
- `com.unity.multiplayer.center` `1.0.0`

> Remove these for single-player projects to reduce compile time if rewind networking is unnecessary.

## Tooling
- `com.unity.ide.visualstudio`
- `com.unity.test-framework`

## Optional Unity Modules
Unity automatically includes many `com.unity.modules.*` packages when creating a project. DOTS-focused templates can disable those not required by gameplay:
- `com.unity.modules.accessibility`
- `com.unity.modules.ai`
- `com.unity.modules.androidjni`
- `com.unity.modules.animation`
- `com.unity.modules.assetbundle`
- `com.unity.modules.audio`
- `com.unity.modules.cloth`
- `com.unity.modules.director`
- `com.unity.modules.imageconversion`
- `com.unity.modules.imgui`
- `com.unity.modules.jsonserialize`
- `com.unity.modules.particlesystem`
- `com.unity.modules.physics`
- `com.unity.modules.physics2d`
- `com.unity.modules.screencapture`
- `com.unity.modules.terrain`
- `com.unity.modules.terrainphysics`
- `com.unity.modules.tilemap`
- `com.unity.modules.ui`
- `com.unity.modules.uielements`
- `com.unity.modules.umbra`
- `com.unity.modules.unityanalytics`
- `com.unity.modules.unitywebrequest`
- `com.unity.modules.unitywebrequestassetbundle`
- `com.unity.modules.unitywebrequestaudio`
- `com.unity.modules.unitywebrequesttexture`
- `com.unity.modules.unitywebrequestwww`
- `com.unity.modules.vehicles`
- `com.unity.modules.video`
- `com.unity.modules.vr`
- `com.unity.modules.wind`
- `com.unity.modules.xr`

Disable any unused modules from *Project Settings → Player → Other Settings → Configuration → Scripting Define Symbols / Api Compatibility* or by editing the manifest to improve build times.

## Candidate for Removal (not required for the DOTS template)
- `com.unity.visualscripting` (removed in baseline template)
- `com.unity.timeline` (removed in baseline template)
- `com.unity.cloud.gltfast` (removed in baseline template)
- `com.unity.collab-proxy` (removed in baseline template)
- `com.unity.ai.navigation` (removed in baseline template)

These packages ship with the default URP template but are not used by the DOTS environment. Consider removing them from `Packages/manifest.json` to reduce import time.

## External Packages
- `com.coplaydev.coplay` (beta) – ensure version stability or pin to a commit hash when shipping.
- `com.coplaydev.unity-mcp` – required for MCP tooling; document version/purpose for future upgrades.

Revisit this audit before producing releases to ensure no redundant dependencies remain.
