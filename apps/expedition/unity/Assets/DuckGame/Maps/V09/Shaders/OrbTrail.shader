Shader "Duck Game/V09/Orb Trail" {
Properties {_Glow("Orb hue",Color)=(.2,2,2,1)}
SubShader {Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
Pass {Tags{"LightMode"="UniversalForward"} Blend SrcAlpha One ZWrite Off Cull Off
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
CBUFFER_START(UnityPerMaterial)
float4 _Glow;
CBUFFER_END
struct A{float4 p:POSITION;float2 uv:TEXCOORD0;float4 col:COLOR;};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float4 col:COLOR;};
V vert(A a){V v;v.p=TransformObjectToHClip(a.p.xyz);v.uv=a.uv;v.col=a.col;return v;}
half4 frag(V i):SV_Target{float edge=pow(saturate(1-abs(i.uv.y*2-1)),2);return half4(_Glow.rgb*.45,edge*.55*i.col.a);}
ENDHLSL
}}}
