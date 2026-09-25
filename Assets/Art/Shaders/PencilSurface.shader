Shader "Confiscated/Pencil Surface"
{
    Properties
    {
        [MainTexture] _BaseMap("Surface illustration", 2D) = "white" {}
        [MainColor] _BaseColor("Paper tint", Color) = (1,1,1,1)
        _EdgeMap("Drawn navy pencil strip", 2D) = "white" {}
        _EdgeColor("Edge ink", Color) = (.045,.065,.13,1)
        _EdgeWidth("Edge band in metres", Float) = .009
        _EdgeRepeat("Metres per edge strip", Float) = .45
        _SideRect("Plain surface crop for narrow sides (UV)", Vector) = (.1,.1,.2,.2)
        _SideWorldSize("Crop coverage in metres", Vector) = (.2,.2,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        float _SchoolDarkness;
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_EdgeMap); SAMPLER(sampler_EdgeMap);
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST, _BaseColor, _EdgeColor, _SideRect, _SideWorldSize;
        float _EdgeWidth, _EdgeRepeat;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float2 faceMetres : TEXCOORD1;
            float2 faceSize : TEXCOORD2;
            float4 color : COLOR;
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float2 faceMetres : TEXCOORD1;
            float2 faceSize : TEXCOORD2;
            float3 positionWS : TEXCOORD3;
            half3 normalWS : TEXCOORD4;
            float side : TEXCOORD5;
            half fog : TEXCOORD6;
        };
        Varyings Vert(Attributes input)
        {
            Varyings o;
            VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
            o.positionCS = p.positionCS; o.positionWS = p.positionWS;
            o.normalWS = TransformObjectToWorldNormal(input.normalOS);
            o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            o.faceMetres = input.faceMetres; o.faceSize = input.faceSize;
            o.side = input.color.r; o.fog = ComputeFogFactor(p.positionCS.z);
            return o;
        }
        half PencilEdge(float distanceMetres, float alongMetres)
        {
            // Real scanned/generated pencil marks following each mesh face boundary, not a noise overlay.
            float x = .36 + .35 * saturate(distanceMetres / max(_EdgeWidth,.0001));
            half pigment = SAMPLE_TEXTURE2D(_EdgeMap, sampler_EdgeMap,
                float2(x, alongMetres / max(_EdgeRepeat,.01))).r;
            return (1 - smoothstep(.12,.92,pigment)) * (1 - smoothstep(_EdgeWidth*.88,_EdgeWidth,distanceMetres));
        }
        half4 Frag(Varyings i) : SV_Target
        {
            float2 uv = i.uv;
            if(i.side > .5)
                uv = _SideRect.xy + frac(i.faceMetres / max(_SideWorldSize.xy,.001)) * _SideRect.zw;
            half3 paper = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb * _BaseColor.rgb;
            float2 d = max(0,min(i.faceMetres,i.faceSize-i.faceMetres));
            half ink = max(PencilEdge(d.x,i.faceMetres.y), PencilEdge(d.y,i.faceMetres.x+.137));
            half3 albedo = lerp(paper,_EdgeColor.rgb,ink*.94);
            half3 n = normalize(i.normalWS);
            Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
            half3 diffuse = SampleSH(n) + mainLight.color * saturate(dot(n,mainLight.direction))
                * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
            #ifdef _ADDITIONAL_LIGHTS
            // Fluorescent fixtures and blackout beams both illuminate pencil surfaces.
            // Forward+ must process local lights in normal mode as well as during an outage.
            {
            InputData inputData = (InputData)0;
            inputData.positionWS = i.positionWS;
            inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
            uint count = GetAdditionalLightsCount();
            LIGHT_LOOP_BEGIN(count)
            {
                Light light = GetAdditionalLight(lightIndex,i.positionWS,half4(1,1,1,1));
                diffuse += light.color * saturate(dot(n,light.direction)) * light.distanceAttenuation * light.shadowAttenuation;
            }
            LIGHT_LOOP_END
            }
            #endif
            return half4(MixFog(albedo * diffuse,i.fog),1);
        }
        half4 DepthFrag(Varyings i) : SV_Target { return 0; }
        half4 NormalFrag(Varyings i) : SV_Target { return half4(normalize(i.normalWS),0); }
        float3 _LightDirection, _LightPosition;
        Varyings ShadowVert(Attributes input)
        {
            Varyings o = Vert(input);
            float3 direction = _LightDirection;
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
            direction = normalize(_LightPosition-o.positionWS);
            #endif
            o.positionCS = TransformWorldToHClip(ApplyShadowBias(o.positionWS,o.normalWS,direction));
            #if UNITY_REVERSED_Z
            o.positionCS.z = min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
            #else
            o.positionCS.z = max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
            #endif
            return o;
        }
        ENDHLSL
        Pass
        {
            Name "PencilForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment NormalFrag
            ENDHLSL
        }
    }
}
