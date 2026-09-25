Shader "Confiscated/Sketch Puddle"
{
    Properties
    {
        _BaseMap("Pencil grain",2D)="white"{}
        _Tint("Water tint",Color)=(.21,.32,.31,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1,-1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _Tint;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1; half radial:TEXCOORD2; };
            Varyings vert(Attributes i)
            {
                Varyings o;o.positionWS=TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);o.uv=i.uv;o.radial=i.color.r;return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float3 view=SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                float3 normal=normalize(float3(.012*sin(i.uv.y*24),1,.009*cos(i.uv.x*21)));
                half grain=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).r;
                half fresnel=pow(1-saturate(dot(view,normal)),3);
                half edge=1-smoothstep(.975,1,i.radial);
                half outline=exp(-pow((i.radial-.959)/.009,2))*(.35+grain*.65);
                half3 env=GlossyEnvironmentReflection(reflect(-view,normal),.12,1);
                Light mainLight=GetMainLight();
                half spec=pow(saturate(dot(normal,SafeNormalize(view+mainLight.direction))),160);
                // Broken, narrow pencil-white reflections; restrained enough to reveal the tile pattern.
                float curve=i.uv.y-.37-.035*sin(i.uv.x*9);
                half streak=exp(-pow(curve/.006,2))*smoothstep(.18,.27,i.uv.x)*(1-smoothstep(.57,.73,i.uv.x));
                float curve2=i.uv.y-.65-.022*sin(i.uv.x*13);
                streak+=.55*exp(-pow(curve2/.004,2))*smoothstep(.43,.49,i.uv.x)*(1-smoothstep(.7,.8,i.uv.x));
                half3 ambient=max(SampleSH(normal),half3(.18,.18,.18));
                half3 color=_Tint.rgb*(.85+grain*.15)*ambient;
                color=lerp(color,env,.3+fresnel*.5);
                color+=mainLight.color*spec*.65;
                color=lerp(color,half3(.73,.80,.75),streak*.7);
                color=lerp(color,half3(.12,.19,.18),outline*.5);
                half alpha=(.22+fresnel*.22+outline*.18+streak*.38+spec*.22)*edge;
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
