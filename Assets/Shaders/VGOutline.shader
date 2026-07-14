// VG/Outline — inverted-hull ink line. 2XKO-style: thick, confident, warm-black
// (never pure black — reads as ink, not aliasing). Second material slot on any renderer.
//
// Hard/split-normal meshes (AI-generated characters) tear the hull apart if offset
// along raw normals. For those, the asset pipeline bakes SMOOTHED normals into the
// vertex COLOR channel (xyz*0.5+0.5) and the material sets _UseBakedNormals = 1.
Shader "VG/Outline"
{
    Properties
    {
        _OutlineColor("Ink Color", Color) = (0.09, 0.06, 0.08, 1)
        _OutlineWidth("Width (m)", Range(0, 0.12)) = 0.035
        [Toggle] _UseBakedNormals("Baked smooth normals (vertex color)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Outline"
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                half _OutlineWidth;
                half _UseBakedNormals;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 dirOS = _UseBakedNormals > 0.5
                    ? normalize(IN.color.rgb * 2.0 - 1.0)
                    : IN.normalOS;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nWS = normalize(TransformObjectToWorldNormal(dirOS));
                posWS += nWS * _OutlineWidth;
                OUT.positionHCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
