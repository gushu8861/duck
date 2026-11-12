Shader "Duck Game/Orb Glow URP" {
Properties { _Glow("Linear glow",Vector)=(.25,2,4,1) }
SubShader{Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
Pass{Tags{"LightMode"="UniversalForward"}
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
float4 _Glow;struct A{float4 positionOS:POSITION;};float4 vert(A a):SV_POSITION{return TransformObjectToHClip(a.positionOS.xyz);}half4 frag():SV_Target{return _Glow;}
ENDHLSL
}}
}
