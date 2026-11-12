Shader "Duck Game/V09/Tailored Toon" {
Properties {_Tint("Linear palette",Vector)=(1,1,1,1) _Sheen("Soft sheen",Float)=.08 _Fabric("Fabric grain",Float)=0}
SubShader {Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
Pass {Tags{"LightMode"="UniversalForward"} Cull Back
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
CBUFFER_START(UnityPerMaterial)
float4 _Tint;float _Sheen;float _Fabric;
CBUFFER_END
struct A{float4 p:POSITION;float3 n:NORMAL;};struct V{float4 p:SV_POSITION;float3 n:TEXCOORD0;float3 w:TEXCOORD1;float3 o:TEXCOORD2;};
V vert(A a){V v;v.p=TransformObjectToHClip(a.p.xyz);v.w=TransformObjectToWorld(a.p.xyz);v.o=a.p.xyz;v.n=TransformObjectToWorldNormal(a.n);return v;}
half4 frag(V i):SV_Target{float3 n=normalize(i.n),v=GetWorldSpaceNormalizeViewDir(i.w),l=normalize(float3(0,.76,.65));float d=dot(n,l);float shade=.59+.21*smoothstep(.08,.34,d)+.20*smoothstep(.43,.69,d);float spec=pow(saturate(dot(n,normalize(l+v))),32)*_Sheen;float rim=pow(1-saturate(dot(n,v)),3)*.045;return half4(_Tint.rgb*shade+spec+_Tint.rgb*rim,1);}
ENDHLSL
}}}
