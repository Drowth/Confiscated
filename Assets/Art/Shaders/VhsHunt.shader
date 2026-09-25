Shader "Confiscated/VHS Hunt"
{
    // Full-screen pass. Driven entirely by the globals _VhsIntensity (0..1) and _VhsTime, set by HuntVhsEffect.
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always
        Pass
        {
            Name "VHS Hunt"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float _VhsIntensity, _VhsTime, _VhsExitRed;
            float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord; float k = saturate(_VhsIntensity);
                if (k < .003 && _VhsExitRed < .003) return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float t = _VhsTime;
                // Tape tracking: one soft band drifts down the picture and tears the lines inside it sideways.
                float band = frac(uv.y * .9 + t * .23); float tear = smoothstep(.0, .035, band) * (1 - smoothstep(.035, .11, band));
                // Every scanline wobbles a little; a few lines at a time glitch further.
                float row = floor(uv.y * 240); float wobble = (Hash(float2(row, floor(t * 24))) - .5) * .0035;
                float glitch = step(.985, Hash(float2(floor(uv.y * 38), floor(t * 9)))) * (Hash(float2(row, t)) - .5) * .05;
                float shift = (wobble + glitch + tear * .018 * sin(t * 31 + uv.y * 40)) * k;
                // Colour bleed: red and blue are read slightly either side of green.
                float bleed = (.0028 + tear * .006) * k;
                half3 colour;
                colour.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, float2(uv.x + shift + bleed, uv.y)).r;
                colour.g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, float2(uv.x + shift, uv.y)).g;
                colour.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, float2(uv.x + shift - bleed, uv.y)).b;
                // Washed-out tape: a touch less saturation, scanlines, grain, a bright smear in the tracking band.
                half grey = dot(colour, half3(.299, .587, .114)); colour = lerp(colour, grey.xxx, .22 * k);
                colour *= 1 - .10 * k * (.5 + .5 * sin(uv.y * 1500));
                colour += (Hash(uv * float2(640, 360) + t * 60) - .5) * .09 * k;
                colour += tear * .10 * k;
                // Gentle, irregular brightness drift. Deliberately small and slow: no strobing.
                colour *= 1 + (Hash(float2(floor(t * 7), 3)) - .5) * .06 * k;
                // Darkened tape edges.
                float2 edge = abs(uv - .5) * 2; colour *= 1 - .22 * k * smoothstep(.75, 1.15, max(edge.x, edge.y * .9));
                // A continuous red wash, strongest at the edges; retain detail in the player's route.
                float red = saturate(_VhsExitRed);
                float edgeRed = smoothstep(.2, 1.05, length(edge * .72));
                half luminance = dot(colour, half3(.299, .587, .114));
                half3 redTape = half3(max(colour.r, luminance * 1.12 + .10), colour.g * .35, colour.b * .30);
                colour = lerp(colour, redTape, red * (.42 + .48 * edgeRed));
                return half4(saturate(colour), 1);
            }
            ENDHLSL
        }
    }
}
