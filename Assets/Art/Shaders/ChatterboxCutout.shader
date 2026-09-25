Shader "Confiscated/Chatterbox Cutout"
{
    Properties
    {
        [MainTexture] _BaseMap("Character artwork", 2D) = "white" {}
        _TalkMap("Open mouth source", 2D) = "white" {}
        _MouthOpen("Open mouth", Float) = 0
        _MouthRect("Mouth region (UV)", Vector) = (.443,.684,.158,.062)
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _Cutoff("Alpha cutoff", Range(0,1)) = 0.5
        [Toggle] _MagentaKey("Remove sprite chroma background", Float) = 0
        [Toggle] _DirectionalViews("Use front and back atlas views", Float) = 0
        _FrontRect("Front atlas rectangle", Vector) = (0,0,1,1)
        _BackRect("Back atlas rectangle", Vector) = (0,0,1,1)
        _ChaseTorsoWidth("Chase torso artwork correction", Range(.5,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Back
        ZWrite On
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DarkModeLighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_TalkMap);
            SAMPLER(sampler_TalkMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
                half _MouthOpen;
                float4 _MouthRect;
                half _MagentaKey;
                half _DirectionalViews;
                float4 _FrontRect;
                float4 _BackRect;
                half _ChaseTorsoWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fog : TEXCOORD1; float3 positionWS : TEXCOORD2; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 centre = TransformObjectToWorld(float3(0,0,0));
                float3 objectRight = mul((float3x3)unity_ObjectToWorld, float3(1,0,0));
                float3 objectUp = mul((float3x3)unity_ObjectToWorld, float3(0,1,0));
                float width = length(objectRight);
                float height = length(objectUp);

                // Face this draw's camera on the GPU. Parent AI yaw and a previous
                // camera's transform update must never expose mirrored artwork.
                float3 away = centre - GetCameraPositionWS();
                away.y = 0;
                away = dot(away, away) > 0.000001 ? normalize(away) : float3(0,0,1);
                float3 right = cross(float3(0,1,0), away);
                float3 up = float3(0,1,0);

                // Preserve the child animation's small Z roll, stretch and bounce.
                float rollSin = clamp(objectRight.y / max(width, 0.00001), -1, 1);
                float rollCos = sqrt(saturate(1 - rollSin * rollSin));
                float3 animatedRight = right * rollCos + up * rollSin;
                float3 animatedUp = up * rollCos - right * rollSin;
                float3 world = centre + animatedRight * input.positionOS.x * width
                    + animatedUp * input.positionOS.y * height;
                output.positionCS = TransformWorldToHClip(world); output.positionWS = world;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                // The generated reaching frames have a narrower coat. Widen only
                // the torso sample; keep the head and feet at the same size as every
                // other chase frame so the character does not pulse between steps.
                float torso = smoothstep(.25,.32,output.uv.y) * (1-smoothstep(.72,.82,output.uv.y));
                output.uv.x = .5 + (output.uv.x-.5) * lerp(1,_ChaseTorsoWidth,torso);
                if (_DirectionalViews > .5)
                {
                    // Keep the actor's facing independent of the draw-facing quad: a pupil looks at the teacher.
                    float3 facing = TransformObjectToWorldDir(float3(0,0,1));
                    float4 rect = dot(facing, -away) >= 0 ? _FrontRect : _BackRect;
                    output.uv = rect.xy + input.uv * rect.zw;
                }
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // Keep the body, silhouette and pencil grain identical in both talking states.
            // The generated open image contributes only a feathered patch around the mouth.
            half4 Artwork(float2 uv)
            {
                half4 closed = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                if (_MouthOpen > .5)
                {
                    float2 edge = min(uv - _MouthRect.xy, _MouthRect.xy + _MouthRect.zw - uv);
                    float mask = smoothstep(0,.007,min(edge.x,edge.y));
                    half4 talking = SAMPLE_TEXTURE2D(_TalkMap, sampler_TalkMap, uv);
                    closed.rgb = lerp(closed.rgb, talking.rgb, mask);
                }
                return closed * _BaseColor;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 colour = Artwork(input.uv);
                // Optional source-art key. Existing alpha sprites leave this disabled.
                // Both red and blue must dominate green, preserving burgundy clothing and navy ink.
                if (_MagentaKey > .5) clip(.10 - min(colour.r - colour.g, colour.b - colour.g));
                clip(colour.a - _Cutoff);
                colour.rgb = MixFog(colour.rgb * SchoolIllustrationLight(input.positionWS,input.positionCS), input.fog);
                return half4(colour.rgb, 1);
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                half4 colour = Artwork(input.uv);
                if (_MagentaKey > .5) clip(.10 - min(colour.r - colour.g, colour.b - colour.g));
                clip(colour.a - _Cutoff);
                return 0;
            }
            half4 NormalsFrag(Varyings input) : SV_Target
            {
                DepthFrag(input);
                float3 centre=TransformObjectToWorld(float3(0,0,0));
                float3 normal=GetCameraPositionWS()-centre;normal.y=0;normal=normalize(normal);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct=PackNormalOctQuadEncode(normal);
                    return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
                #else
                    return half4(normal,0);
                #endif
            }
        ENDHLSL
        Pass
        {
            Name "Character"
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            ENDHLSL
        }
        // The depth prepass must use the same billboard and alpha silhouette as the colour pass.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull Off ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment NormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            ENDHLSL
        }
    }
}
