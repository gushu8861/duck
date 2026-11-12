Shader "Duck Game/V10/Short Impact" {
Properties {_Tint("Tint",Color)=(1,1,1,1) _Ring("Ring",Float)=1}
SubShader {Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
Pass {Tags{"LightMode"="UniversalForward"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
CBUFFER_START(UnityPerMaterial)
float4 _Tint;float _Ring;
CBUFFER_END
struct A{float4 p:POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;};
V vert(A a){V v;v.p=TransformObjectToHClip(a.p.xyz);v.uv=a.uv;v.c=a.c;return v;}
half4 frag(V i):SV_Target{float radius=length(i.uv-.5);float width=max(fwidth(radius),.005);float ring=1-smoothstep(.012,.012+width,abs(radius-.36));float alpha=lerp(sin(saturate(i.uv.y)*3.14159),ring,_Ring);return half4(_Tint.rgb,_Tint.a*alpha*lerp(i.c.a,1,_Ring));}
ENDHLSL
}}}
