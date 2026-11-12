Shader "Duck Game/V11/Layered Toon"
{
    Properties
    {
        _V10Visibility("Camera visibility", Range(0,1)) = 1
        _Tint("Linear palette", Vector) = (1,1,1,1)
        _Sheen("Sheen", Float) = .008
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "Layered Toon"
            Tags { "LightMode"="UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _V10Visibility;
                float4 _Tint;
                float _Sheen;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float noise = frac(52.9829189 * frac(dot(input.positionCS.xy, float2(.06711056, .00583715))));
                clip(_V10Visibility - noise);

                float3 normal = normalize(input.normalWS);
                float3 light = normalize(float3(0, .76, .65));
                float diffuse = dot(normal, light);
                // Keep the plateaus flat while softening the boundary by one raster pixel.
                float antialias = clamp(fwidth(diffuse) * .8, .006, .055);
                float shade = .56 + .22 * smoothstep(.20 - antialias, .20 + antialias, diffuse)
                                  + .22 * smoothstep(.55 - antialias, .55 + antialias, diffuse);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float sheen = pow(saturate(dot(normal, SafeNormalize(light + view))), 36) * _Sheen;
                return half4(_Tint.rgb * shade + sheen, 1);
            }
            ENDHLSL
        }
    }
}
