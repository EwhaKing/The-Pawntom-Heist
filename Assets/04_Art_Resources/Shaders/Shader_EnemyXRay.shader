// 경비견 투시 실루엣. 벽·물체 뒤에 가려진 부분만 붉게 그린다(ZTest Greater).
// 튜토리얼 6단계 "순찰 동선 안내" 대사 동안 EnemyXRayGlow 가 경비견 몸에 이 셰이더의 복제 렌더러를 덧씌운다.
Shader "Pawntom/Enemy XRay"
{
    Properties
    {
        [HDR] _Color ("Silhouette Color", Color) = (1.0, 0.12, 0.08, 0.9)
        _RimPower ("Rim Power", Range(0.5, 6)) = 1.6
        _Pulse ("Pulse Strength", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "XRay"
            Tags { "LightMode" = "UniversalForward" }

            // 앞에 무언가(벽 등) 있어서 깊이 버퍼가 더 가까운 곳만 그린다.
            ZTest Greater
            ZWrite Off
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _RimPower;
                half _Pulse;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positions.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 viewDir = normalize(input.viewDirWS);
                half rim = pow(1.0h - saturate(dot(normal, viewDir)), _RimPower);
                half pulse = 1.0h + _Pulse * sin(_Time.y * 5.0h);
                half3 color = _Color.rgb * (0.7h + 0.9h * rim) * pulse;
                half alpha = saturate(_Color.a * (0.65h + 0.35h * rim));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
