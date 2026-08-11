// Packs your separate Roughness + Metallic maps into ONE texture:
// R = metallic, A = smoothness (1 - roughness).
// This is the exact layout URP's Lit shader expects for its
// "Metallic Map" slot when Smoothness Source is set to
// "Metallic Alpha" - so no custom lit shader is needed at all.
Shader "Hidden/Mech/CombineMetallicSmoothness"
{
    Properties
    {
        _RoughnessMap ("Roughness Map", 2D) = "white" {}
        _MetallicMap ("Metallic Map", 2D) = "black" {}
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

            sampler2D _RoughnessMap;
            sampler2D _MetallicMap;

            fixed4 frag(v2f_img IN) : SV_Target
            {
                fixed roughness = tex2D(_RoughnessMap, IN.uv).r;
                fixed metallic = tex2D(_MetallicMap, IN.uv).r;
                fixed smoothness = 1.0 - roughness;

                return fixed4(metallic, metallic, metallic, smoothness);
            }
            ENDCG
        }
    }
}
