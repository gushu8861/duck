Shader "Duck Game/V10/Ready Pearl" {
Properties {_V10Visibility("Camera visibility",Range(0,1))=1 _Glow("Orb hue",Color)=(.2,2,2,1)}
SubShader {Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
Pass {Tags{"LightMode"="UniversalForward"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Back
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
CBUFFER_START(UnityPerMaterial)
float _V10Visibility;float4 _Glow;
CBUFFER_END
struct A{float4 p:POSITION;float3 n:NORMAL;};struct V{float4 p:SV_POSITION;float3 n:TEXCOORD0;float3 w:TEXCOORD1;float3 o:TEXCOORD2;};
V vert(A a){V v;v.p=TransformObjectToHClip(a.p.xyz);v.w=TransformObjectToWorld(a.p.xyz);v.o=a.p.xyz;v.n=TransformObjectToWorldNormal(a.n);return v;}
half4 frag(V i):SV_Target{float3 n=normalize(i.n),v=GetWorldSpaceNormalizeViewDir(i.w);float f=saturate(dot(n,v));float core=pow(f,12);float ribbon=pow(.5+.5*sin(i.o.y*28+i.o.x*19+sin(i.o.z*21)*1.4-_Time.y*2.2),7);float3 c=_Glow.rgb*(.11+.27*f+.12*ribbon)+core*.64;float alpha=smoothstep(0,.24,f)*.94;return half4(c,alpha*_V10Visibility);}
ENDHLSL
}}}
