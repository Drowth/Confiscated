Shader "Confiscated/Pencil Sky"
{
 Properties { _MainTex("Sketched sky",2D)="white"{} _Exposure("Paper brightness",Range(.1,2))=1 }
 SubShader
 {
  Tags{"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
  Cull Off ZWrite Off
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;float _Exposure;
   // Global, 0..1: the final escape turns the outdoors to night (WindowWatchers).
   float _OutdoorNight;
   struct v2f {float4 pos:SV_POSITION;float3 direction:TEXCOORD0;};
   v2f vert(float4 vertex:POSITION){v2f o;o.pos=UnityObjectToClipPos(vertex);o.direction=vertex.xyz;return o;}
   half4 frag(v2f i):SV_Target
   {
    float3 d=normalize(i.direction);
    float2 uv=float2(atan2(d.x,d.z)/6.2831853+.5,asin(clamp(d.y,-1,1))/3.14159265+.5);
    half3 day=tex2D(_MainTex,uv).rgb*_Exposure;
    return half4(lerp(day,day*half3(.006,.009,.018)+half3(.001,.0015,.003),_OutdoorNight),1);
   }
   ENDHLSL
  }
 }
}
