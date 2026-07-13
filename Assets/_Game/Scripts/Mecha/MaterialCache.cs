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
    }
}
