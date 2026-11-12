Shader "Duck Game/V11/Ink Outline"
{
    Properties { [HideInInspector] _Tint("Hit Tint", Vector) = (1,1,1,1) _V10Visibility("Camera visibility",Range(0,1))=1  _WidthPixels("Outline width (pixels)", Range(0,3)) = 1.15 _Ink("Ink", Color) = (.026,.022,.043,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1" }
        Pass
        {
            Name "ThinOutline"
            Tags { "LightMode"="UniversalForward" }
            Cull Front
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _V10Visibility;float _WidthPixels;
            half4 _Ink;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            float4 vert(Attributes input) : SV_POSITION
            {
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                float4 clip = TransformWorldToHClip(world);
                float3 viewNormal = TransformWorldToViewDir(normal, true);
                float2 projected = mul((float3x3)UNITY_MATRIX_P, viewNormal).xy;
                // Normalize in pixel space so aspect ratio and camera distance do not change the normalized width.
                float2 pixelDirection = projected * _ScaledScreenParams.xy;
                pixelDirection /= max(length(pixelDirection), 0.0001);
                clip.xy += pixelDirection * (2.0 * _WidthPixels / float2(1080.0 * _ScaledScreenParams.x / _ScaledScreenParams.y, 1080.0)) * clip.w;
                return clip;
            }
            half4 frag(float4 p:SV_POSITION) : SV_Target { float noise=frac(52.9829189*frac(dot(p.xy,float2(.06711056,.00583715))));clip(_V10Visibility-noise);return _Ink; }
            ENDHLSL
        }
    }
}

