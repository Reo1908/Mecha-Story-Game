using UnityEngine;
using System.Collections.Generic;

// A renderer on a different GameObject (e.g. a shoulder part moved
// by code instead of being rigged) that should receive the exact
// same baked textures as this part's main renderer.
[System.Serializable]
public class MechRendererTarget
{
    public Renderer renderer;
    public int materialSlotIndex;
}

// Replaces MechPartController from the custom-shader approach.
// This bakes the color/detail/shade mix into a RenderTexture once
// per color change (not per frame), and bakes roughness+metallic
// into a packed texture once per texture change. The result feeds
// a completely stock "Universal Render Pipeline/Lit" material via
// MaterialPropertyBlock, so it only ever affects this renderer.
//
// ONE-TIME SETUP on the shared material asset this part uses:
//  1. Shader: Universal Render Pipeline/Lit
//  2. Assign ANY texture into the "Metallic Map" slot once (this
//     turns on the keyword that makes URP read from a metallic
//     map at all - the script overrides the actual texture later)
//  3. Set "Smoothness Source" (under the Metallic Map field) to
//     "Metallic Alpha"
//  4. Assign ANY texture into the Normal Map slot once, to enable
//     normal mapping - the script overrides it per-instance after
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class MechTextureBaker : MonoBehaviour
{
    [Header("Shared Color Profile")]
    public MechColorProfile colorProfile;

    [Header("Combine Shaders (drag from Project)")]
    public Shader combineAlbedoShader;
    public Shader combineMetallicSmoothnessShader;

    [Header("Color Map")]
    public Texture2D colorMap;

    [Header("Detail")]
    public Texture2D detailTexture;

    [Header("Shade (baked AO / shadow, multiply)")]
    public Texture2D shadeTexture;
    [Range(0f, 1f)] public float shadeStrength = 1f;

    [Header("Normal (no baking needed)")]
    public Texture2D normalMap;
    [Range(0f, 2f)] public float normalStrength = 1f;

    [Header("Roughness / Metallic (baked together once)")]
    public Texture2D roughnessMap;
    public Texture2D metallicMap;

    [Header("Target (auto-filled if empty)")]
    public Renderer targetRenderer;

    [Tooltip("Which material slot on the renderer this part uses (0 = first slot, 1 = second, etc). Use this when armor and decal share one Renderer with two materials.")]
    public int materialSlotIndex = 0;

    [Header("Other Parts To Match (e.g. a shoulder moved by code)")]
    [Tooltip("Any other renderers that should receive the exact same baked textures as this part - useful for parts kept as separate GameObjects instead of being rigged.")]
    public List<MechRendererTarget> additionalTargets = new List<MechRendererTarget>();

    private Material _combineAlbedoMat;
    private Material _combineMetallicMat;
    private RenderTexture _albedoRT;
    private RenderTexture _metallicSmoothRT;
    private MaterialPropertyBlock _block;

    private static readonly int BaseMapID = Shader.PropertyToID("_BaseMap");
    private static readonly int MetallicGlossMapID = Shader.PropertyToID("_MetallicGlossMap");
    private static readonly int BumpMapID = Shader.PropertyToID("_BumpMap");
    private static readonly int BumpScaleID = Shader.PropertyToID("_BumpScale");

    private void OnEnable()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        _block ??= new MaterialPropertyBlock();

        if (colorProfile != null)
            colorProfile.OnChanged += BakeAlbedo;

        BakeMetallicSmoothness();
        BakeAlbedo();
        ApplyNormal();
    }

    private void OnDisable()
    {
        if (colorProfile != null)
            colorProfile.OnChanged -= BakeAlbedo;

        ReleaseRT(ref _albedoRT);
        ReleaseRT(ref _metallicSmoothRT);
    }

    private void OnValidate()
    {
        if (colorProfile != null)
        {
            colorProfile.OnChanged -= BakeAlbedo;
            colorProfile.OnChanged += BakeAlbedo;
        }

        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        _block ??= new MaterialPropertyBlock();

        BakeMetallicSmoothness();
        BakeAlbedo();
        ApplyNormal();
    }

    /// <summary>
    /// Runs the color-mixing combine shader into a RenderTexture and
    /// assigns it as this renderer's Base Map. Call this only when a
    /// color or paint-related texture actually changes.
    /// </summary>
    public void BakeAlbedo()
    {
        if (colorMap == null || combineAlbedoShader == null || targetRenderer == null) return;

        if (_combineAlbedoMat == null)
            _combineAlbedoMat = new Material(combineAlbedoShader);

        EnsureRT(ref _albedoRT, colorMap.width, colorMap.height, sRGB: true, filterMode: colorMap.filterMode);

        _combineAlbedoMat.SetTexture("_ColorMap", colorMap);

        Texture2D camo = colorProfile != null ? colorProfile.camoTexture : null;
        bool camoR = colorProfile != null && colorProfile.useCamoForMain;
        bool camoG = colorProfile != null && colorProfile.useCamoForSecondary;
        bool camoB = colorProfile != null && colorProfile.useCamoForDetail;

        _combineAlbedoMat.SetTexture("_CamoMap", camo != null ? camo : Texture2D.blackTexture);
        _combineAlbedoMat.SetFloat("_UseCamoR", camoR ? 1f : 0f);
        _combineAlbedoMat.SetFloat("_UseCamoG", camoG ? 1f : 0f);
        _combineAlbedoMat.SetFloat("_UseCamoB", camoB ? 1f : 0f);

        if (colorProfile != null)
        {
            _combineAlbedoMat.SetColor("_Color1", colorProfile.mainColor);
            _combineAlbedoMat.SetColor("_Color2", colorProfile.secondaryColor);
            _combineAlbedoMat.SetColor("_Color3", colorProfile.detailColor);
        }

        _combineAlbedoMat.SetTexture("_DetailTex", detailTexture != null ? detailTexture : Texture2D.blackTexture);
        _combineAlbedoMat.SetTexture("_ShadeTex", shadeTexture != null ? shadeTexture : Texture2D.whiteTexture);
        _combineAlbedoMat.SetFloat("_ShadeStrength", shadeStrength);

        Graphics.Blit(null, _albedoRT, _combineAlbedoMat);
        RenderTexture.active = null;

        ApplyToAllTargets(block => block.SetTexture(BaseMapID, _albedoRT));
    }

    /// <summary>
    /// Packs Roughness + Metallic into one texture (R=metallic,
    /// A=smoothness) and assigns it. This doesn't depend on color,
    /// so it only needs to re-run when the maps themselves change.
    /// </summary>
    public void BakeMetallicSmoothness()
    {
        if (metallicMap == null || roughnessMap == null || combineMetallicSmoothnessShader == null || targetRenderer == null) return;

        if (_combineMetallicMat == null)
            _combineMetallicMat = new Material(combineMetallicSmoothnessShader);

        EnsureRT(ref _metallicSmoothRT, metallicMap.width, metallicMap.height, sRGB: false, filterMode: metallicMap.filterMode);

        _combineMetallicMat.SetTexture("_RoughnessMap", roughnessMap);
        _combineMetallicMat.SetTexture("_MetallicMap", metallicMap);

        Graphics.Blit(null, _metallicSmoothRT, _combineMetallicMat);
        RenderTexture.active = null;

        ApplyToAllTargets(block => block.SetTexture(MetallicGlossMapID, _metallicSmoothRT));
    }

    /// <summary>
    /// The normal map needs no baking - just assign it and its
    /// strength directly.
    /// </summary>
    public void ApplyNormal()
    {
        if (normalMap == null || targetRenderer == null) return;

        ApplyToAllTargets(block =>
        {
            block.SetTexture(BumpMapID, normalMap);
            block.SetFloat(BumpScaleID, normalStrength);
        });
    }

    /// <summary>
    /// Applies the same property-block changes to this part's own
    /// renderer AND every renderer listed in additionalTargets - so
    /// a separate, non-rigged part (like a shoulder) stays in sync.
    /// </summary>
    private void ApplyToAllTargets(System.Action<MaterialPropertyBlock> setProperties)
    {
        if (targetRenderer != null)
        {
            targetRenderer.GetPropertyBlock(_block, materialSlotIndex);
            setProperties(_block);
            targetRenderer.SetPropertyBlock(_block, materialSlotIndex);
        }

        if (additionalTargets == null) return;

        foreach (var target in additionalTargets)
        {
            if (target?.renderer == null) continue;

            target.renderer.GetPropertyBlock(_block, target.materialSlotIndex);
            setProperties(_block);
            target.renderer.SetPropertyBlock(_block, target.materialSlotIndex);
        }
    }

    private static void EnsureRT(ref RenderTexture rt, int width, int height, bool sRGB, FilterMode filterMode)
    {
        if (rt != null && rt.width == width && rt.height == height && rt.sRGB == sRGB && rt.filterMode == filterMode)
            return;

        if (rt != null)
        {
            if (RenderTexture.active == rt)
                RenderTexture.active = null;

            rt.Release();
            if (Application.isPlaying) Destroy(rt);
            else DestroyImmediate(rt);
        }

        var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 0)
        {
            sRGB = sRGB
        };

        rt = new RenderTexture(descriptor)
        {
            filterMode = filterMode,
            wrapMode = TextureWrapMode.Repeat
        };
        rt.Create();
    }

    private static void ReleaseRT(ref RenderTexture rt)
    {
        if (rt == null) return;

        if (RenderTexture.active == rt)
            RenderTexture.active = null;

        rt.Release();
        rt = null;
    }
}
