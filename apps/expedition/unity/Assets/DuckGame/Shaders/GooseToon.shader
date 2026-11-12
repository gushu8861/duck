Shader "Duck Game/Toon URP" {
Properties { _Tint("Linear palette",Vector)=(1,1,1,1) }
SubShader {Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
Pass {Name "Toon" Tags{"LightMode"="UniversalForward"} Cull Back
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
CBUFFER_START(UnityPerMaterial)
float4 _Tint;
CBUFFER_END
struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};struct V{float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;};
V vert(A a){V o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(a.normalOS);return o;}
half4 frag(V i):SV_Target{float d=dot(normalize(i.normalWS),normalize(float3(0,.76,.65)));float s=d<.2?.55:(d<.55?.8:1);return float4(_Tint.rgb*s,1);}
ENDHLSL
}}
}
