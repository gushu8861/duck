Shader "Duck Game/Outline URP" {
Properties { _Width("World width",Float)=0.003 _Ink("Linear ink",Vector)=(.018,.016,.032,1) }
SubShader {Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1"}
Pass {Name "Outline" Tags{"LightMode"="UniversalForward"} Cull Front ZWrite On
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
CBUFFER_START(UnityPerMaterial)
float _Width;float4 _Ink;
CBUFFER_END
struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;};
float4 vert(A a):SV_POSITION{float3 p=TransformObjectToWorld(a.positionOS.xyz);float3 n=TransformObjectToWorldNormal(a.normalOS);return TransformWorldToHClip(p+n*_Width);}
half4 frag():SV_Target{return _Ink;}
ENDHLSL
}}
}
