Shader "Space4X/AsteroidChunkPalette"
{
    Properties
    {
        _MaterialPalette("Material Palette", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

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
                return tint * _BaseColor;
            }
            ENDHLSL
        }
    }
}
