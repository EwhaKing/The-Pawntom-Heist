// 미니맵/CCTV 용 평면(조명 무관) 단색 셰이더.
//
// - 조명, 그림자, 포그의 영향을 받지 않는다. (그림자 캐스터 패스도 없다)
// - 직교(orthographic) 카메라에서만 그린다. 미니맵/CCTV 카메라는 직교, 플레이어 카메라는 원근이라
//   MinimapFloor 복제본이 게임 화면에 겹쳐 그려지지 않는다.
// - [MainColor] 라서 코드의 material.color(격벽 열림/닫힘 색 등)가 그대로 먹는다.
// - _UsePlayerStyle 이 켜진 머티리얼은, 플레이어 미니맵 카메라(MinimapCameraStyle 이 전역값
//   _PawntomMinimapPlayerStyle 을 1 로 켠 동안)에서만 _PlayerColor 로 그린다. CCTV 지도는 _BaseColor 그대로.
Shader "Pawntom/Minimap Flat"
{
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _PlayerColor ("Player minimap color", Color) = (1, 1, 1, 1)
        [Toggle] _UsePlayerStyle ("Use player minimap color on player minimap", Float) = 0
        [Toggle] _ShowInPerspective ("Show in perspective cameras (debug)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "MinimapFlat"
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _PlayerColor;
                half _UsePlayerStyle;
                half _ShowInPerspective;
            CBUFFER_END

            float _PawntomMinimapPlayerStyle;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // unity_OrthoParams.w == 1 이면 직교 카메라. 아니면 클립 공간 밖으로 보내 그리지 않는다.
                bool hide = unity_OrthoParams.w < 0.5 && _ShowInPerspective < 0.5;
                if (hide)
                {
                    output.positionCS = float4(2.0, 2.0, 2.0, 1.0);
                }
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                bool player = _UsePlayerStyle > 0.5 && _PawntomMinimapPlayerStyle > 0.5;
                return half4(player ? _PlayerColor.rgb : _BaseColor.rgb, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
