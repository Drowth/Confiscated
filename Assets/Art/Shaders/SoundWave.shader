Shader "Confiscated/Sound Wave"
{
    // Pencil-grained vertex-colour strokes drawn through walls: a door slam's sound spreading across the school (DoorSlam).
    Properties { [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 8 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZTest [_ZTest]
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 colour:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 colour:COLOR; float3 positionWS:TEXCOORD1; };
            Varyings Vert(Attributes i) { Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.uv=i.uv;o.colour=i.colour;return o; }
            float Hash(float3 p) { return frac(sin(dot(p,float3(12.9898,78.233,37.719)))*43758.5453); }
            half4 Frag(Varyings i):SV_Target
            {
                // Crayon on toothy paper: fine grain, diagonal screen-space hatching and a ragged stroke edge.
                float2 px=i.positionCS.xy;
                float tooth=Hash(float3(floor(px*.5),0));
                float hatch=saturate(sin((px.x+px.y)*.55)*.5+.75);
                float edge=1-abs(i.uv.y*2-1);edge=saturate(edge*2.4-tooth*.9);
                half4 c=i.colour;c.a*=saturate(tooth*.7+.45)*hatch*edge;
                return c;
            }
            ENDHLSL
        }
    }
}
