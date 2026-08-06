Shader "Custom/RocketExhaustBlackbody"
{
    Properties
    {
        [MainTexture] _MainTex ("Flame Texture (Alpha = Heat)", 2D) = "white" {}
        _ScrollSpeed ("Scroll Speed", Float) = 1.0
        _Brightness  ("Brightness", Float) = 1.0
        _OverallAlpha ("Overall Transparency", Range(0, 1)) = 1.0

        // Blackbody stops, coldest -> hottest.
        // Defaults: deep red (coldest / tip) -> orange -> yellow -> white -> blue-white (hottest / root)
        [HDR]_ColdColor    ("Coldest Color (low alpha)",  Color) = (0.6, 0.05, 0.0, 1)
        [HDR]_WarmColor    ("Warm Color",                 Color) = (1.0, 0.35, 0.0, 1)
        [HDR]_MidColor     ("Mid Color (yellow)",         Color) = (1.0, 0.85, 0.3, 1)
        [HDR]_HotColor     ("Hot Color (white)",          Color) = (1.0, 1.0, 1.0, 1)
        [HDR]_HottestColor ("Hottest Color (high alpha)", Color) = (0.55, 0.75, 1.0, 1)

        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5  // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10 // OneMinusSrcAlpha
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _ScrollSpeed;
                float  _Brightness;
                float  _OverallAlpha;
                float4 _ColdColor;
                float4 _WarmColor;
                float4 _MidColor;
                float4 _HotColor;
                float4 _HottestColor;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            // 5-stop blackbody-style gradient, t = 0 (coldest) -> 1 (hottest)
            float3 BlackbodyRamp(float t)
            {
                t = saturate(t);
                float3 col = lerp(_ColdColor.rgb,   _WarmColor.rgb,    saturate(t * 4.0 - 0.0));
                col        = lerp(col,              _MidColor.rgb,    saturate(t * 4.0 - 1.0));
                col        = lerp(col,              _HotColor.rgb,    saturate(t * 4.0 - 2.0));
                col        = lerp(col,              _HottestColor.rgb,saturate(t * 4.0 - 3.0));
                return col;
            }

            float4 frag (Varyings IN) : SV_Target
            {
                float2 scrolledUV = IN.uv;
                scrolledUV.y = frac(scrolledUV.y - _Time.y * _ScrollSpeed);

                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, scrolledUV);

                // _OverallAlpha now feeds the heat calculation itself, not just the final alpha,
                // so dialing it down pulls the color down the blackbody ramp toward the cold end
                // in addition to fading the flame out.
                float heat = saturate(tex.a * _OverallAlpha);
                float3 color = BlackbodyRamp(heat) * _Brightness;

                return float4(color, heat);
            }
            ENDHLSL
        }
    }
}
