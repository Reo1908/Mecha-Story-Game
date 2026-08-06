Shader "Hidden/RetroFX/PhosphorTrail"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D_X(_HistoryTex);
        TEXTURE2D_X(_DecayedTex);
        TEXTURE2D_X(_FreshTex);
        TEXTURE2D_X(_TrailTex);

        float _Threshold;
        float _Knee;
        float _Clamp;
        float _Decay;
        float2 _TexelSize;
        float4 _Tint;
        float _Intensity;

        half3 Prefilter(half3 c)
        {
            half brightness = max(c.r, max(c.g, c.b));
            half soft = brightness - _Threshold + _Knee;
            soft = clamp(soft, 0.0h, 2.0h * _Knee);
            soft = soft * soft / (4.0h * _Knee + 1e-5h);
            half contribution = max(soft, brightness - _Threshold);
            contribution /= max(brightness, 1e-5h);
            c *= contribution;
            c = min(c, _Clamp.xxx);
            return max(c, 0.0h);
        }

        // Pass 0: threshold the current frame (also serves as the initial downsample).
        half4 FragPrefilter(Varyings i) : SV_Target
        {
            half3 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord).rgb;
            return half4(Prefilter(c), 1.0h);
        }

        // Pass 1: fade the previous frame's history buffer.
        half4 FragDecay(Varyings i) : SV_Target
        {
            half3 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord).rgb;
            return half4(c * _Decay, 1.0h);
        }

        // Pass 2: combine this frame's bright pixels with the decayed
        // history. max() (rather than add) keeps the trail from runaway
        // brightening on repeated frames while still holding onto strong
        // highlights as they fade.
        half4 FragCombine(Varyings i) : SV_Target
        {
            half3 fresh = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord).rgb;
            half3 decayed = SAMPLE_TEXTURE2D_X(_DecayedTex, sampler_LinearClamp, i.texcoord).rgb;
            return half4(max(fresh, decayed), 1.0h);
        }

        // Pass 3: subtract this frame's OWN bright pixels from the
        // accumulated history before displaying it. Without this, a light
        // that isn't moving would show its full brightness added back on
        // top of the scene every single frame - i.e. it'd just look like a
        // second bloom layer. This leaves only the genuine "leftover glow"
        // from frames where the light *used* to be but isn't right now,
        // so a static light contributes nothing extra and only motion
        // trails show.
        half4 FragResidual(Varyings i) : SV_Target
        {
            half3 hist = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord).rgb;
            half3 fresh = SAMPLE_TEXTURE2D_X(_FreshTex, sampler_LinearClamp, i.texcoord).rgb;
            return half4(max(hist - fresh, 0.0h), 1.0h);
        }

        // Pass 4: small 4-tap blur, used only for display - NOT fed back
        // into history, so it doesn't compound into runaway softness over time.
        half4 FragSoften(Varyings i) : SV_Target
        {
            float2 uv = i.texcoord;
            float2 o = _TexelSize;
            half3 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( o.x,  o.y)).rgb;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-o.x,  o.y)).rgb;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( o.x, -o.y)).rgb;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-o.x, -o.y)).rgb;
            c *= 0.2h;
            return half4(c, 1.0h);
        }

        half4 FragComposite(Varyings i) : SV_Target
        {
            half3 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord).rgb;
            half3 trail = SAMPLE_TEXTURE2D_X(_TrailTex, sampler_LinearClamp, i.texcoord).rgb;
            trail *= _Tint.rgb * _Intensity;
            return half4(scene + trail, 1.0h);
        }
        ENDHLSL

        Pass
        {
            Name "Prefilter"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPrefilter
            ENDHLSL
        }

        Pass
        {
            Name "Decay"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDecay
            ENDHLSL
        }

        Pass
        {
            Name "Combine"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragCombine
            ENDHLSL
        }

        Pass
        {
            Name "Residual"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragResidual
            ENDHLSL
        }

        Pass
        {
            Name "Soften"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragSoften
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            ENDHLSL
        }
    }
}
