using UnityEngine;

// This is a saveable ASSET (right click in Project window ->
// Create -> Mech -> Color Profile). Assign the same profile to
// multiple MechPartController / DecalController components so
// painting once updates every part that shares it, and so you
// can save/reuse color presets between mechs.
[CreateAssetMenu(fileName = "New Mech Color Profile", menuName = "Mech/Color Profile")]
public class MechColorProfile : ScriptableObject
{
    [Header("Trim Colors")]
    public Color mainColor = Color.white;
    public Color secondaryColor = Color.white;
    public Color detailColor = Color.white;

    [Header("Decal Color (4th trim)")]
    public Color decalColor = Color.white;

    [Header("Camo")]
    [Tooltip("Used instead of the flat color on any channel below that's checked on.")]
    public Texture2D camoTexture;
    public bool useCamoForMain;
    public bool useCamoForSecondary;
    public bool useCamoForDetail;

    // Anything listening (armor parts, decals) subscribes to this
    // and refreshes only when it actually fires - not every frame.
    public event System.Action OnChanged;

    public void NotifyChanged()
    {
        OnChanged?.Invoke();
    }
}
