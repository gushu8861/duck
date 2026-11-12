Shader "Duck Game/V09/Omit Band Outline" {
SubShader {Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Geometry+1"}
Pass {Tags{"LightMode"="UniversalForward"} ColorMask 0 ZWrite Off Cull Back
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
float4 vert(float4 p:POSITION):SV_POSITION{return TransformObjectToHClip(p.xyz);}
half4 frag():SV_Target{return 0;}
ENDHLSL
}}}
