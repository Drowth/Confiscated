Shader "Confiscated/Character Cutout"
{
    Properties
    {
        [MainTexture] _BaseMap("Character artwork", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _Cutoff("Alpha cutoff", Range(0,1)) = 0.5
        [Toggle] _MagentaKey("Remove sprite chroma background", Float) = 0
        [Toggle] _DirectionalViews("Use front and back atlas views", Float) = 0
        _FrontRect("Front atlas rectangle", Vector) = (0,0,1,1)
        _BackRect("Back atlas rectangle", Vector) = (0,0,1,1)
        _ChaseTorsoWidth("Chase torso artwork correction", Range(.5,1)) = 1
        [HideInInspector] _PoseWidth("Artwork framing width", Range(.5,1.5)) = 1
        [HideInInspector] _EyeGlowLeft("Left eye UV / radius", Vector) = (0,0,0,0)
        [HideInInspector] _EyeGlowRight("Right eye UV / radius", Vector) = (0,0,0,0)
        [HideInInspector] _MouthOpen("Talking mouth shown", Float) = 0
        [HideInInspector] _MouthRect("Mouth centre / half size (texture UV)", Vector) = (0,0,0,0)
        [HideInInspector] _MouthSkinUV("Skin sample (texture UV)", Vector) = (0,0,0,0)
        [HideInInspector] _MouthStyle("0 draws an open mouth, 1 draws a closed one", Float) = 0
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
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
                half _MagentaKey;
                half _DirectionalViews;
                float4 _FrontRect;
                float4 _BackRect;
                half _ChaseTorsoWidth;
                half _PoseWidth;
                float4 _EyeGlowLeft, _EyeGlowRight;
                float4 _MouthRect, _MouthSkinUV;
                half _MouthOpen, _MouthStyle;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fog : TEXCOORD1; float3 positionWS : TEXCOORD2; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 centre = TransformObjectToWorld(float3(0,0,0));
                float3 objectRight = mul((float3x3)unity_ObjectToWorld, float3(1,0,0));
                float3 objectUp = mul((float3x3)unity_ObjectToWorld, float3(0,1,0));
                float width = length(objectRight) * _PoseWidth;
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

            half EyeGlow(float2 uv,float4 eye)
            {
                if(eye.z<=0||eye.w<=0)return 0;
                float distance=length((uv-eye.xy)/eye.zw);
                float aa=max(fwidth(distance),.06);
                // A hot red centre and soft red halo, drawn on the actual face so walls still occlude it.
                return 3.5*(1-smoothstep(.45-aa,.85+aa,distance))+.6*exp2(-distance*distance*1.7);
            }
            // Two-frame talking, Mr Men style: TalkingMouth flips _MouthOpen while the character speaks.
            // Drawn rather than painted so every character gets it without a second piece of artwork.
            half3 TalkingMouth(half3 art, float2 uv)
            {
                if (_MouthOpen < .5 || _MouthRect.z <= 0) return art;
                float2 d = (uv - _MouthRect.xy) / _MouthRect.zw;
                float r = length(d);
                float aa = max(fwidth(r), .02);
                // Linear-space values: the project renders in linear, so these are the art's near-black navy ink, a dark maroon and a tongue pink.
                half3 ink = half3(.005,.006,.014);
                if (_MouthStyle < .5)
                {
                    // Closed artwork: a dark oval large enough to hide the drawn line, with a tongue and an ink rim.
                    float2 t = (d - float2(0,-.62)) / float2(.62,.5);
                    half3 inside = lerp(half3(.022,.003,.004), half3(.47,.065,.06), 1 - smoothstep(.85, 1, length(t)));
                    half3 mouth = lerp(inside, ink, smoothstep(.74 - aa, .8, r));
                    return lerp(mouth, art, smoothstep(1 - aa, 1 + aa, r));
                }
                // Open artwork: paper over it with the character's own skin tone and draw a closed smile.
                half3 skin = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, _MouthSkinUV.xy, 3).rgb * _BaseColor.rgb;
                // A flat patch reads as a sticker on speckled pencil skin: scatter a few darker flecks like the paper around it.
                float2 cell = floor(uv * float2(1024,1536) / 3);
                float fleck = frac(sin(dot(cell, float2(12.9898,78.233))) * 43758.5453);
                skin *= 1 - .22 * step(.9, fleck) - .06 * frac(fleck * 7.31);
                float curve = abs(d.y - (-.25 + .45 * d.x * d.x));
                float smile = (1 - smoothstep(.12, .12 + aa * 2, curve)) * (1 - smoothstep(.7, .8, abs(d.x)));
                return lerp(lerp(skin, ink, smile), art, smoothstep(1, 1.25, r));
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 colour = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                // Optional source-art key. Existing alpha sprites leave this disabled.
                // Both red and blue must dominate green, preserving burgundy clothing and navy ink.
                if (_MagentaKey > .5) clip(.10 - min(colour.r - colour.g, colour.b - colour.g));
                clip(colour.a - _Cutoff);
                colour.rgb = TalkingMouth(colour.rgb, input.uv);
                colour.rgb = MixFog(colour.rgb * SchoolIllustrationLight(input.positionWS,input.positionCS), input.fog);
                half glow=max(EyeGlow(input.uv,_EyeGlowLeft),EyeGlow(input.uv,_EyeGlowRight))*_SchoolDarkness;
                colour.rgb=lerp(colour.rgb,half3(glow,glow*.006,glow*.002),saturate(glow));
                return half4(colour.rgb, 1);
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                half4 colour = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
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
