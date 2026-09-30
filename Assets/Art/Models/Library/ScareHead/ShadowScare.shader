Shader "Confiscated/Shadow Scare Charcoal"
{
    Properties { _BaseMap("Generated colour",2D)="white"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            struct A { float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;float3 positionWS:TEXCOORD1;float2 uv:TEXCOORD2;float3 local:TEXCOORD3; };
            V vert(A i) { V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.uv=i.uv;o.local=i.positionOS.xyz;return o; }
            half4 frag(V i):SV_Target
            {
                float3 map=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                float lum=dot(map,float3(.299,.587,.114));
                float3 p=i.local*100;
                float grain=frac(sin(dot(floor(p*1900),float3(12.9898,78.233,37.719)))*43758.5453);
                float hatch=smoothstep(.2,.48,abs(sin((p.y+p.z*.72)*440+sin(p.z*83)*.65)));
                float cross=smoothstep(.12,.4,abs(sin((p.y-p.z*.6)*570)));
                float light=.30+.70*saturate(dot(normalize(i.normalWS),normalize(float3(-.4,.65,-.7))));
                float rim=pow(1-saturate(abs(dot(normalize(i.normalWS),normalize(_WorldSpaceCameraPos-i.positionWS)))),3);
                float shade=pow(saturate(lum*1.7),.8)*light;
                float3 ink=lerp(float3(.004,.007,.016),float3(.13,.17,.23),shade);
                ink*=lerp(.7,1,hatch)*lerp(.8,1,cross)*lerp(.65,1.25,grain);
                ink+=rim*float3(.035,.055,.09);
                // Suppress the generated orange eyes; separate white slit geometry supplies the glow.
                float eye=smoothstep(.035,.13,map.r-max(map.g,map.b))*smoothstep(.12,.35,map.r);
                float socket=1-smoothstep(.7,1.15,length(float2((abs(p.y)-.11)/.065,(p.z-.14)/.055)));
                eye=max(eye,socket*smoothstep(.05,.15,abs(p.x)));
                return half4(lerp(ink,float3(.005,.008,.015),eye),1);
            }
            ENDHLSL
        }
    }
}
