using System.Collections.Generic;
using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Erstellt und cached zur Laufzeit einfache URP-Materialien pro Farbe,
    /// damit Blöcke gleicher Farbe ein Material teilen.
    /// </summary>
    public static class MaterialCache
    {
        static readonly Dictionary<Color, Material> LitMaterials = new Dictionary<Color, Material>();
        static readonly Dictionary<Color, Material> UnlitMaterials = new Dictionary<Color, Material>();
        static Shader _litShader;
        static Shader _unlitShader;

        public static Material GetLit(Color color)
        {
            if (!LitMaterials.TryGetValue(color, out Material material) || material == null)
            {
                if (_litShader == null)
                    _litShader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(_litShader) { color = color };
                LitMaterials[color] = material;
            }
            return material;
        }

        public static Material GetUnlit(Color color)
        {
            if (!UnlitMaterials.TryGetValue(color, out Material material) || material == null)
            {
                if (_unlitShader == null)
                    _unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                material = new Material(_unlitShader) { color = color };
                UnlitMaterials[color] = material;
            }
            return material;
        }

        /// <summary>
        /// Neues (nicht gecachtes) transparentes Unlit-Material, z. B. für den
        /// Energieschild. Nicht gecacht, damit der Nutzer Alpha/Farbe animieren
        /// kann, ohne andere Objekte zu beeinflussen.
        /// </summary>
        public static Material CreateTransparentUnlit(Color color)
        {
            if (_unlitShader == null)
                _unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(_unlitShader) { color = color };

            // URP-Unlit auf "Surface Type: Transparent" umstellen.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return material;
        }
    }
}
