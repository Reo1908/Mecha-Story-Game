using UnityEngine;
using System.Collections.Generic;

// For decals we don't need a custom shader - Unity's own
// Universal Render Pipeline/Lit shader already supports a base
// texture with alpha, alpha clipping, a tint color, and uniform
// Metallic/Smoothness sliders. This just drives those built-in
// properties per-instance via MaterialPropertyBlock.
//
// Setup once on the shared decal material asset:
//  - Shader: Universal Render Pipeline/Lit
//  - Surface Type: Opaque, Alpha Clipping: On
//  - Assign your decal texture (with alpha) as the Base Map
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class DecalController : MonoBehaviour
{
    [Header("Shared Color Profile")]
    public MechColorProfile colorProfile;

    [Header("Decal Texture")]
    public Texture2D decalTexture;

    [Header("Surface")]
    [Range(0f, 1f)] public float roughness = 0.5f;
    [Range(0f, 1f)] public float metallic = 0f;

    [Header("Target (auto-filled if empty)")]
    public Renderer targetRenderer;

    [Tooltip("Which material slot on the renderer this decal uses (0 = first slot, 1 = second, etc). Use this when armor and decal share one Renderer with two materials.")]
    public int materialSlotIndex = 1;

    [Header("Other Parts To Match (e.g. a shoulder moved by code)")]
    [Tooltip("Any other renderers that should receive the exact same decal properties as this one.")]
    public List<MechRendererTarget> additionalTargets = new List<MechRendererTarget>();

    private MaterialPropertyBlock _block;

    private static readonly int BaseMapID = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int MetallicID = Shader.PropertyToID("_Metallic");
    private static readonly int SmoothnessID = Shader.PropertyToID("_Smoothness");

    private void OnEnable()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        _block ??= new MaterialPropertyBlock();

        if (colorProfile != null)
            colorProfile.OnChanged += ApplyAll;

        ApplyAll();
    }

    private void OnDisable()
    {
        if (colorProfile != null)
            colorProfile.OnChanged -= ApplyAll;
    }

    private void OnValidate()
    {
        if (colorProfile != null)
        {
            colorProfile.OnChanged -= ApplyAll;
            colorProfile.OnChanged += ApplyAll;
        }

        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        _block ??= new MaterialPropertyBlock();
        ApplyAll();
    }

    public void ApplyAll()
    {
        if (targetRenderer == null || _block == null) return;

        ApplyToRenderer(targetRenderer, materialSlotIndex);

        if (additionalTargets == null) return;

        foreach (var target in additionalTargets)
        {
            if (target?.renderer != null)
                ApplyToRenderer(target.renderer, target.materialSlotIndex);
        }
    }

    private void ApplyToRenderer(Renderer renderer, int slotIndex)
    {
        renderer.GetPropertyBlock(_block, slotIndex);

        if (decalTexture != null) _block.SetTexture(BaseMapID, decalTexture);
        if (colorProfile != null) _block.SetColor(BaseColorID, colorProfile.decalColor);
        _block.SetFloat(MetallicID, metallic);
        _block.SetFloat(SmoothnessID, 1f - roughness);

        renderer.SetPropertyBlock(_block, slotIndex);
    }
}
