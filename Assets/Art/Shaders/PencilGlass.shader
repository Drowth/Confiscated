Shader "Confiscated/Pencil Glass"
{
 Properties
 {
  [MainTexture]_BaseMap("Blue grey pencil strokes",2D)="white"{}
  _EdgeMap("Drawn edge strip",2D)="white"{}
  _Tint("Glass tint",Color)=(.60,.73,.78,1)
  _Opacity("Clear glass opacity",Range(0,.5))=.065
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
  Blend SrcAlpha OneMinusSrcAlpha
  Cull Off ZWrite Off
  Pass
  {
   Tags{"LightMode"="SRPDefaultUnlit"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   TEXTURE2D(_EdgeMap);SAMPLER(sampler_EdgeMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST;half4 _Tint;float _Opacity;
   CBUFFER_END
   struct A{float4 p:POSITION;float2 uv:TEXCOORD0;};
   struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
   V vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.uv=a.uv;return o;}
   half4 frag(V i):SV_Target
   {
    half3 pencil=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv*1.5).rgb;
    float diagonal=frac(i.uv.x+i.uv.y*.55);
    float streak=(1-smoothstep(.025,.060,abs(diagonal-.30)))+(1-smoothstep(.008,.022,abs(diagonal-.43)));
    float edge=min(min(i.uv.x,1-i.uv.x),min(i.uv.y,1-i.uv.y));
    float pigment=1-SAMPLE_TEXTURE2D(_EdgeMap,sampler_EdgeMap,float2(.48,i.uv.x+i.uv.y*3)).r;
    float ink=(1-smoothstep(.002,.012,edge))*pigment;
    float opacity=saturate(_Opacity+streak*(.14+(1-pencil.r)*.16)+ink*.6);
    half3 colour=lerp(lerp(_Tint.rgb,pencil,.7),half3(.10,.16,.25),ink);
    return half4(colour,opacity);
   }
   ENDHLSL
  }
 }
}
