// Not a real lit shader - this only ever runs through Graphics.Blit
// into a RenderTexture. It does the exact color-mixing math from our
// earlier custom shader, but the result becomes a plain texture that
// gets fed into Unity's own URP/Lit shader afterward.
Shader "Hidden/Mech/CombineAlbedo"
{
    Properties
    {
        _ColorMap ("Color Map (R=Main G=Secondary B=Detail)", 2D) = "white" {}
        _CamoMap ("Camo Map", 2D) = "white" {}
        _UseCamoR ("Use Camo R", Float) = 0
        _UseCamoG ("Use Camo G", Float) = 0
        _UseCamoB ("Use Camo B", Float) = 0
        _Color1 ("Main Color", Color) = (1,1,1,1)
        _Color2 ("Secondary Color", Color) = (1,1,1,1)
        _Color3 ("Detail Color", Color) = (1,1,1,1)
        _DetailTex ("Detail Texture (RGBA)", 2D) = "black" {}
        _ShadeTex ("Shade Texture (B&W)", 2D) = "white" {}
        _ShadeStrength ("Shade Strength", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _ColorMap;
            sampler2D _CamoMap;
            sampler2D _DetailTex;
            sampler2D _ShadeTex;

            float _UseCamoR;
            float _UseCamoG;
            float _UseCamoB;
            float4 _Color1;
            float4 _Color2;
            float4 _Color3;
            float _ShadeStrength;

            fixed4 frag(v2f_img IN) : SV_Target
            {
                fixed4 mask = tex2D(_ColorMap, IN.uv);
                fixed4 camo = tex2D(_CamoMap, IN.uv);

                fixed3 colR = lerp(_Color1.rgb, camo.rgb, _UseCamoR);
                fixed3 colG = lerp(_Color2.rgb, camo.rgb, _UseCamoG);
                fixed3 colB = lerp(_Color3.rgb, camo.rgb, _UseCamoB);

                fixed3 painted = fixed3(0, 0, 0);
                painted = lerp(painted, colR, mask.r);
                painted = lerp(painted, colG, mask.g);
                painted = lerp(painted, colB, mask.b);

                fixed4 detail = tex2D(_DetailTex, IN.uv);
                fixed3 withDetail = lerp(painted, detail.rgb, detail.a);

                fixed shade = tex2D(_ShadeTex, IN.uv).r;
                fixed shadeMul = lerp(1.0, shade, _ShadeStrength);
                fixed3 albedo = withDetail * shadeMul;

                return fixed4(albedo, 1.0);
            }
            ENDCG
        }
    }
}
