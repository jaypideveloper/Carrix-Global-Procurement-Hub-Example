// Minimal URP unlit shader used by the network globe.
// Multiplies vertex colour by _BaseColor, with an optional fresnel rim and configurable blending,
// so one shader covers the ocean sphere, atmosphere halo, land dots, pins, rings and arcs.
Shader "ProcurementHub/Unlit"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _RimColor ("Rim Color", Color) = (0,0,0,0)
        _RimPower ("Rim Power", Float) = 3
        _RimInvert ("Rim Invert (0/1)", Float) = 0
        _RimAlpha ("Rim Drives Alpha (0/1)", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        [Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

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
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float4 color       : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                float _RimPower;
                float _RimInvert;
                float _RimAlpha;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 c = IN.color * _BaseColor;
                // Meshes without normals (e.g. line strips) get a zero normal; avoid NaNs from normalize(0).
                float3 n = IN.normalWS / max(length(IN.normalWS), 1e-5);
                float3 v = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float ndv = abs(dot(n, v));
                float rim = lerp(1.0 - ndv, ndv, _RimInvert);
                rim = pow(saturate(rim), _RimPower);
                c.rgb += _RimColor.rgb * _RimColor.a * rim;
                c.a = lerp(c.a, c.a * rim, _RimAlpha);
                return c;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
