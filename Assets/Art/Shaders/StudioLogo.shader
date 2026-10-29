Shader "Confiscated/UI/StudioLogo"
{
 Properties
 {
  [PerRendererData] _MainTex ("Logo", 2D) = "white" {}
  _Color ("Tint", Color) = (1,1,1,1)
 }
 SubShader
 {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
  Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
  Blend SrcAlpha OneMinusSrcAlpha
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
   struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
   sampler2D _MainTex;
   fixed4 _Color;
   v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o; }
   fixed4 frag(v2f i):SV_Target
   {
    fixed4 c=tex2D(_MainTex,i.uv);
    // The source's low-alpha contour contains pale matte pixels.
    // Blend those into the existing brown outline before applying the UI fade.
    c.rgb=lerp(fixed3(.055,.022,.008),c.rgb,smoothstep(.85,.98,c.a));
    c.a=smoothstep(.08,.96,c.a);
    return c*i.color;
   }
   ENDCG
  }
 }
}