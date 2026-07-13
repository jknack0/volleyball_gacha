// VG/Toon — cel shading for the grey-box anime pass. Inspiration: 2XKO's read —
// hard 2-band terminator, hue-shifted (cool) shadows instead of plain darkening,
// strong rim light so characters pop off the stage. All values [tunable] in-inspector.
Shader "VG/Toon"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _ShadowTint("Shadow Tint (hue-shifted)", Color) = (0.45, 0.38, 0.62, 1)
        _Bands("Shade Bands", Range(1, 4)) = 2
        _MidPoint("Terminator Position", Range(0.05, 0.95)) = 0.5
        _RimColor("Rim Color", Color) = (1.0, 0.96, 0.87, 1)
        _RimPower("Rim Tightness", Range(0.5, 8)) = 3.0
        _RimStrength("Rim Strength", Range(0, 1)) = 0.45
        _Ambient("Ambient Floor", Range(0, 0.6)) = 0.22
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadowTint;
                half _Bands;
                half _MidPoint;
                half4 _RimColor;
                half _RimPower;
                half _RimStrength;
                half _Ambient;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                Light mainLight = GetMainLight();
                half3 n = normalize(IN.normalWS);

                // Hard-banded lambert: shift by the terminator, quantize into _Bands steps.
                half ndl = saturate(dot(n, mainLight.direction));
                half shifted = saturate((ndl - _MidPoint) / max(1.0h - _MidPoint, 0.001h) + 0.5h);
                half band = floor(shifted * _Bands) / max(_Bands - 1.0h, 1.0h);
                band = saturate(band);

                half3 shadowCol = _BaseColor.rgb * _ShadowTint.rgb;
                half3 lit = lerp(shadowCol, _BaseColor.rgb, band) * mainLight.color;

                // Rim: bright edge falloff (the character-pop light).
                half3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                half rim = pow(1.0h - saturate(dot(n, viewDir)), _RimPower) * _RimStrength;

                half3 col = lit + rim * _RimColor.rgb + _BaseColor.rgb * _Ambient;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
