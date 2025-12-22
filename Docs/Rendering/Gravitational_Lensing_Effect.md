# Gravitational Lensing Effect

**Status:** Design Concept / Implementation Guide
**Category:** Space4X - Visual Effects / Post-Processing
**Audience:** Graphics Programmers / Technical Artists
**Created:** 2025-01-17
**Last Updated:** 2025-01-17

---

## Purpose

**Primary Goal:** Implement gravitational lensing visual effect (distorted light around black holes) using screen-space distortion, with a fidelity/cost ladder from cheap (masked screen-space) to expensive (ray-traced geodesics).

**Key Principle:** You can make the "gravitational lensing / distorted light" look pretty cheap, as long as you don't try to do full ray-traced geodesics per pixel.

---

## Fidelity / Cost Ladder

### Tier 1 (Cheap, Recommended): Screen-Space Distortion with Radial Mask

**Sample the already-rendered color buffer and offset UVs around the black hole's projected screen center.**

**Cost:** ~1–3 texture samples per pixel only where the mask applies (you can keep it local).

**Approach:**
- Compute radial mask from black hole screen center + radius
- Only apply distortion where mask > 0 (localized cost)
- Offset UVs based on radial distance from center
- Sample color buffer with warped UVs

**Reference:** [SpaceEngine - Fullscreen Distortion Shader](https://spaceengine.org/) - Ended up doing this as a fullscreen distortion shader to avoid pixelization when you fly close.

### Tier 2 (Still Reasonable): Screen-Space Distortion + Depth Gating + Mip/Blur Control

**Add depth sampling so you only distort background behind the black hole, not foreground ships.**

**Additional Features:**
- Depth sampling (request Depth texture from URP)
- Skip distortion if pixel is closer than black hole depth (foreground protection)
- Optional: build/use mipmaps for color buffer to reduce star shimmer when UVs warp hard

**Tradeoff:** SpaceEngine mentions mipmaps reduce moiré/shimmer but may cost performance.

**Reference:** [SpaceEngine - Mipmap Control](https://spaceengine.org/)

### Tier 3 (Heavy): Per-Pixel Ray Integration / Raymarch

**Integrate Schwarzschild geodesics or raymarch density fields (looks awesome, expensive).**

**Approach:**
- ODE integration on GPU for Schwarzschild geodesics
- Ray-traced / integrated per pixel
- Great for "hero shot" or photo mode

**Cost:** Explicitly "ray-traced / integrated" — great reference, not great for "minor effect" budget.

**Reference:** [GitHub - Ray-Traced Black Hole Examples](https://github.com/search?q=black+hole+ray+trace+shader)

---

## Best Approach in Unity 6 URP

**Use URP Full Screen Pass Renderer Feature with a fullscreen shader (Shader Graph or HLSL).**

**It's designed exactly for "inject a full screen effect at a chosen injection point"** and can request Color/Depth/Normals/Motion as needed.

**Reference:** [Unity Documentation - Full Screen Pass Renderer Feature](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/manual/renderer-features/urp-renderer-features.html)

### Implementation Steps

**1. Create Full Screen Pass Renderer Feature:**

```csharp
// ScriptableRendererFeature
public class GravitationalLensingFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material material;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        public bool requiresDepth = true;
    }

    public Settings settings = new Settings();
    private GravitationalLensingPass pass;

    public override void Create()
    {
        pass = new GravitationalLensingPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.material != null)
        {
            pass.ConfigureInput(ScriptableRenderPipelineInput.Color | ScriptableRenderPipelineInput.Depth);
            renderer.EnqueuePass(pass);
        }
    }
}
```

**2. Full Screen Pass (Blit):**

```csharp
// ScriptableRenderPass
private class GravitationalLensingPass : ScriptableRenderPass
{
    private Material material;
    private RenderTargetIdentifier source;
    private RenderTargetHandle tempTexture;

    public GravitationalLensingPass(Settings settings)
    {
        material = settings.material;
        renderPassEvent = settings.renderPassEvent;
        tempTexture.Init("_GravitationalLensingTemp");
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get("Gravitational Lensing");
        source = renderingData.cameraData.renderer.cameraColorTarget;
        
        // Blit with material
        cmd.GetTemporaryRT(tempTexture.id, renderingData.cameraData.cameraTargetDescriptor);
        Blit(cmd, source, tempTexture.Identifier(), material, 0);
        Blit(cmd, tempTexture.Identifier(), source);
        cmd.ReleaseTemporaryRT(tempTexture.id);
        
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}
```

**3. Shader (HLSL or Shader Graph):**

**Key Parameters:**
- `_BlackHoleScreenPos` (float2): Screen-space position of black hole (0-1 UV)
- `_LensingRadius` (float): Screen-space radius of lensing effect
- `_DistortionStrength` (float): How strong the distortion is
- `_ColorBuffer` (Texture2D): Source color texture
- `_DepthTexture` (Texture2D): Depth texture (for depth gating)

**Shader Logic:**
```hlsl
// Compute radial distance from black hole center
float2 screenUV = i.uv;
float2 toCenter = screenUV - _BlackHoleScreenPos;
float dist = length(toCenter);

// Radial mask (0-1)
float mask = smoothstep(_LensingRadius, _LensingRadius * 0.5, dist);

// Skip if outside mask
if (mask <= 0) return color;

// Depth gating (optional Tier 2)
#if _DEPTH_GATING
float depth = SampleSceneDepth(i.uv);
float blackHoleDepth = SampleSceneDepth(_BlackHoleScreenPos);
if (depth < blackHoleDepth) return color; // Foreground, skip distortion
#endif

// Distortion offset (radial, stronger near center)
float distortion = _DistortionStrength * mask * (1.0 / max(dist, 0.001));
float2 offset = normalize(toCenter) * distortion;
float2 distortedUV = screenUV + offset;

// Sample color buffer with warped UVs
float4 lensedColor = SAMPLE_TEXTURE2D(_ColorBuffer, sampler_ColorBuffer, distortedUV);

// Blend (or replace)
return lerp(color, lensedColor, mask);
```

**Reference:** [Unity Documentation - ScriptableRendererFeature](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/manual/urp-renderer-feature-how-to-add.html)

### Key Knobs to Keep It Cheap

**1. Mask It:**
- Compute radial mask from black hole screen center + radius
- Only apply distortion where mask > 0 (localized cost)

**2. Depth Gate:**
- Request Depth and skip distortion if pixel is closer than black hole depth (foreground)
- URP's Full Screen Pass can require Depth

**Reference:** [Unity Documentation - Full Screen Pass Depth](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/manual/renderer-features/urp-renderer-features.html)

**3. One Pass:**
- Try to keep it to one pass (distort + optional ring boost)

**4. Be Aware of Extra Blits:**
- URP historically can introduce extra intermediate blits when adding renderer features
- Check Frame Debugger to verify blit count

**Reference:** [Unity Issue Tracker - Extra Blits](https://issuetracker.unity3d.com/issues/urp-renderer-feature-adds-extra-blits)

---

## HDRP Alternative (If Switching Later)

**If you ever switch to HDRP, it's the same concept via Custom Post Process:**

**Approach:**
- C# volume component + fullscreen shader
- Clear injection points like "BeforeTAA / AfterPostProcess"

**Reference:** [Unity User Manual - HDRP Custom Post Process](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@latest/manual/Custom-Post-Process.html)

---

## Alternative Approaches (Without Fullscreen Shader)

### A) Overlay Sprite / Ring Texture (Ultra Cheap)

**Looks like lensing but doesn't actually distort the background scene.**

**Approach:**
- Draw overlay sprite/ring texture around black hole
- No actual distortion
- Good for distant black holes

**Tradeoff:** Doesn't actually distort background, so close-up shots look fake.

### B) "Lensing Sphere" Mesh (Localized Cost)

**Draw a sphere/billboard around the black hole and in its shader sample the camera color texture with warped UVs.**

**Approach:**
- Sphere/billboard mesh around black hole
- Shader samples camera color texture with warped UVs
- Pixel cost scales with sphere's screen coverage (often less than full screen)

**Tradeoff:** Still needs access to camera color copy (which is effectively the same prerequisite as a post effect).

---

## Practical Recommendation

**Start with Tier 1:** A masked screen-space distortion (URP Full Screen Pass).

**Add depth gating next (Tier 2).**

**Only consider raymarch/geodesic integration (Tier 3) for a "hero shot" or photo mode.**

---

## Integration with Space4X Systems

### Black Hole Component

**Track black hole screen position per frame:**

```csharp
/// <summary>
/// Black hole entity component (for lensing effect).
/// </summary>
public struct BlackHoleComponent : IComponentData
{
    public float3 WorldPosition;
    public float SchwarzschildRadius;      // Event horizon radius
    public float LensingRadiusMultiplier;  // Screen-space radius multiplier
}
```

**System to update shader parameters:**

```csharp
/// <summary>
/// Updates gravitational lensing shader parameters from black hole entities.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct GravitationalLensingUpdateSystem : ISystem
{
    private MaterialPropertyBlock propertyBlock;
    
    public void OnUpdate(ref SystemState state)
    {
        // Find nearest black hole to camera
        var cameraPos = GetCameraPosition();
        Entity nearestBlackHole = Entity.Null;
        float nearestDist = float.MaxValue;
        
        foreach (var (blackHole, transform) in SystemAPI.Query<RefRO<BlackHoleComponent>, RefRO<LocalToWorld>>())
        {
            float dist = math.distance(cameraPos, transform.ValueRO.Position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestBlackHole = // ... get entity
            }
        }
        
        if (nearestBlackHole != Entity.Null)
        {
            // Project to screen space
            var worldPos = // ... get world position
            var screenPos = WorldToScreen(worldPos);
            
            // Update material/shader properties
            propertyBlock.SetVector("_BlackHoleScreenPos", screenPos);
            propertyBlock.SetFloat("_LensingRadius", ComputeScreenRadius(blackHole, dist));
            propertyBlock.SetFloat("_DistortionStrength", ComputeDistortionStrength(dist));
        }
    }
}
```

### Multiple Black Holes

**If multiple black holes are visible:**
- Use additive approach (sum distortions)
- Or pick nearest/largest only
- Or use multiple renderer features (one per black hole, ordered by distance)

---

## Performance Considerations

### Optimization Strategies

1. **Masking:** Only process pixels within lensing radius (early exit in shader)
2. **Depth Gating:** Skip foreground pixels (reduces overdraw)
3. **One Pass:** Minimize blit count (check Frame Debugger)
4. **LOD:** Disable effect for distant black holes (beyond threshold distance)

### Scalability

**Target Performance:**
- Tier 1: <1ms per frame (masked, localized)
- Tier 2: <2ms per frame (with depth gating)
- Tier 3: Photo mode only (5-50ms per frame, acceptable for screenshots)

**Memory Budget:**
- One fullscreen texture (temporary RT)
- Depth texture (shared with other effects)
- Negligible CPU cost (screen-space projection update)

---

## Related Documentation

- **Space4X Render Catalog:** `Docs/Rendering/Space4X_RenderCatalog_TruthSource.md` - Render pipeline overview
- **Unity URP Renderer Features:** https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/manual/renderer-features/urp-renderer-features.html
- **SpaceEngine Lensing:** https://spaceengine.org/ (Reference implementation)

---

**For Graphics Programmers:** Start with Tier 1 (masked screen-space distortion), add depth gating, only consider Tier 3 for photo mode  
**For Technical Artists:** Tune distortion strength, radius, and mask falloff to match desired visual style  
**For Designers:** Use lensing as visual cue for black hole proximity and gravitational influence

