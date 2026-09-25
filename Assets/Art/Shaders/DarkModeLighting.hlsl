#ifndef SCHOOL_DARK_LIGHTING
#define SCHOOL_DARK_LIGHTING
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
float _SchoolDarkness;
half3 SchoolIllustrationLight(float3 positionWS, float4 positionCS)
{
    if(_SchoolDarkness<=0)return half3(1,1,1);
    half3 illumination=half3(.009,.014,.025);
    InputData inputData=(InputData)0;
    inputData.positionWS=positionWS;
    inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(positionCS);
    #if defined(_ADDITIONAL_LIGHTS)
    uint count=GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(count)
        Light l=GetAdditionalLight(lightIndex,positionWS,half4(1,1,1,1));
        illumination+=l.color*l.distanceAttenuation*l.shadowAttenuation;
    LIGHT_LOOP_END
    #endif
    return lerp(half3(1,1,1),min(illumination,half3(1.2,1.2,1.2)),_SchoolDarkness);
}
#endif
