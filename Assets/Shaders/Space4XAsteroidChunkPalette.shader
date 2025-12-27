Shader "Space4X/AsteroidChunkPalette"
{
    Properties
    {
        _MaterialPalette("Material Palette", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _AOMin("AO Min", Range(0,1)) = 0.55
        _OreEmissiveColor("Ore Emissive Color", Color) = (1,0.8,0.4,1)
        _OreEmissiveStrength("Ore Emissive Strength", Range(0,2)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
            };

            TEXTURE2D(_MaterialPalette);
            SAMPLER(sampler_MaterialPalette);
            float4 _BaseColor;
            float _AOMin;
            float4 _OreEmissiveColor;
            float _OreEmissiveStrength;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float id = input.color.r * 255.0;
                float2 uv = float2((id + 0.5) / 256.0, 0.5);
                half4 tint = SAMPLE_TEXTURE2D(_MaterialPalette, sampler_MaterialPalette, uv);
                half ao = lerp(_AOMin, 1.0, input.color.a);
                half4 baseColor = tint * _BaseColor;
                baseColor.rgb *= ao;
                half ore = input.color.g;
                baseColor.rgb += _OreEmissiveColor.rgb * (_OreEmissiveStrength * ore);
                return baseColor;
            }
            ENDHLSL
        }
    }
}
