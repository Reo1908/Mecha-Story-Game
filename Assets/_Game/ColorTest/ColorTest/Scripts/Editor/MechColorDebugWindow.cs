#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Tools > Mech > Color Debug Menu
// Works identically whether the scene is playing or not, since
// it's a plain Editor Window rather than in-game UI. It only
// pushes changes when you actually move a color picker - there's
// no Update() loop or per-frame polling involved.
public class MechColorDebugWindow : EditorWindow
{
    private MechColorProfile _profile;

    [MenuItem("Tools/Mech/Color Debug Menu")]
    private static void ShowWindow()
    {
        var window = GetWindow<MechColorDebugWindow>();
        window.titleContent = new GUIContent("Mech Color Debug");
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Mech Trim Color Debug Menu", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _profile = (MechColorProfile)EditorGUILayout.ObjectField(
            "Color Profile", _profile, typeof(MechColorProfile), false);

        if (_profile == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a Mech Color Profile asset above to edit its colors here.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();

        Color main = EditorGUILayout.ColorField("Main Color", _profile.mainColor);
        Color secondary = EditorGUILayout.ColorField("Secondary Color", _profile.secondaryColor);
        Color detail = EditorGUILayout.ColorField("Detail Color", _profile.detailColor);
        Color decal = EditorGUILayout.ColorField("Decal Color", _profile.decalColor);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Camo", EditorStyles.boldLabel);
        Texture2D camo = (Texture2D)EditorGUILayout.ObjectField(
            "Camo Texture", _profile.camoTexture, typeof(Texture2D), false);
        bool camoR = EditorGUILayout.Toggle("Use Camo for Main", _profile.useCamoForMain);
        bool camoG = EditorGUILayout.Toggle("Use Camo for Secondary", _profile.useCamoForSecondary);
        bool camoB = EditorGUILayout.Toggle("Use Camo for Detail", _profile.useCamoForDetail);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_profile, "Change Mech Colors");

            _profile.mainColor = main;
            _profile.secondaryColor = secondary;
            _profile.detailColor = detail;
            _profile.decalColor = decal;
            _profile.camoTexture = camo;
            _profile.useCamoForMain = camoR;
            _profile.useCamoForSecondary = camoG;
            _profile.useCamoForDetail = camoB;

            EditorUtility.SetDirty(_profile);

            // Fires only now - once per actual edit - so listening
            // parts refresh immediately without any per-frame cost.
            _profile.NotifyChanged();
        }
    }
}
#endif
