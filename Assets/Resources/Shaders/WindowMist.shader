// Heavy drifting mist outside the windows for the final escape (WindowWatchers): hides where the watchers' bodies should be.
// Lives in Resources so player builds include it; the sheets are built at runtime.
Shader "Confiscated/Window Mist"
{
 Properties
 {
  _Color("Mist colour",Color)=(.55,.62,.74,1)
  _Density("Density",Range(0,1))=.8
  _Wisp("How much the swirl and strokes thin it",Range(0,1))=.35
  _Size("Sheet size in metres (xy)",Vector)=(7,1.3,0,0)
  _TopFade("Top fade in metres",Float)=.3
  _Ragged("How far the swirl eats into the top (of the fade)",Range(0,1))=.8
  _SideFade("End fade in metres",Float)=1.2
  _Drift("Drift speed",Float)=.05
  _Seed("Noise offset",Float)=0
  _Fade("Shown",Range(0,1))=1
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10"}
  Blend SrcAlpha OneMinusSrcAlpha
  Cull Off ZWrite Off
  Pass
  {
   Tags{"LightMode"="SRPDefaultUnlit"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   float _SchoolDarkness;
   CBUFFER_START(UnityPerMaterial)
   half4 _Color;float4 _Size;float _Density,_Wisp,_TopFade,_Ragged,_SideFade,_Drift,_Seed,_Fade;
   CBUFFER_END
   struct A{float4 p:POSITION;float2 uv:TEXCOORD0;};
   struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float seed:TEXCOORD1;};
   V vert(A a)
   {
    V o;o.p=TransformObjectToHClip(a.p.xyz);o.uv=a.uv;
    // Every window gets its own swirl.
    o.seed=_Seed+dot(TransformObjectToWorld(float3(0,0,0)).xz,float2(.37,.61));return o;
   }
   float Hash(float2 p){p=frac(p*float2(123.34,456.21));p+=dot(p,p+45.32);return frac(p.x*p.y);}
   float Noise(float2 p)
   {
    float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+float2(1,1)),f.x),f.y);
   }
   float Billow(float2 p){float a=.5,s=0;for(int k=0;k<4;k++){s+=a*Noise(p);p=p*2.03+17.1;a*=.5;}return s/.9375;}
   half4 frag(V i):SV_Target
   {
    float2 m=i.uv*_Size.xy;float t=_Time.y*_Drift;
    float n=Billow(float2(m.x*.55+t+i.seed,m.y*.9-t*.3))*.6+Billow(float2(m.x*1.3-t*1.7+i.seed*2,m.y*1.7+t*.2))*.4;
    // Broken diagonal pencil strokes, so the mist sits with the rest of the drawing.
    float stroke=Noise(float2((m.x+m.y*.6)*34+i.seed,(m.y-m.x*.6)*2.2));
    // The ragged top only ever starts fading early: the sheet's own edge must never show.
    float top=1-smoothstep(_Size.y-_TopFade,_Size.y,m.y+n*_TopFade*_Ragged);
    float ends=smoothstep(0,_SideFade,min(m.x,_Size.x-m.x));
    float alpha=_Density*lerp(1-_Wisp,1,smoothstep(.25,.7,n)*lerp(.6,1,stroke))*top*ends*_Fade;
    // Dark Mode and blackouts leave it faintly moonlit.
    half3 colour=_Color.rgb*lerp(.7,1.12,n)*lerp(.92,1.04,stroke)*lerp(1,.22,_SchoolDarkness);
    return half4(colour,alpha);
   }
   ENDHLSL
  }
 }
}
