using UnityEditor;
using UnityEngine;





public class ScriptOpener : ScriptableObject
{
    public MonoScript targetScript;
    public int lineNumber = 1;
}

[CustomEditor(typeof(ScriptOpener))]
public class ScriptOpenerEditor : Editor
{   
    
    [MenuItem("Tools/Create Script Opener")]
    private static void CreateScriptOpener()
    {
        var asset = ScriptableObject.CreateInstance<ScriptOpener>();
        AssetDatabase.CreateAsset(asset, "Assets/ScriptOpener.asset");
        AssetDatabase.SaveAssets();
        Selection.activeObject = asset;
    }
    public override void OnInspectorGUI()
    {
        ScriptOpener opener = (ScriptOpener)target;

        EditorGUILayout.LabelField("MonoScript Reference", EditorStyles.boldLabel);
        opener.targetScript = (MonoScript)EditorGUILayout.ObjectField("Script", opener.targetScript, typeof(MonoScript), false);

        opener.lineNumber = EditorGUILayout.IntField("Line Number", Mathf.Max(1, opener.lineNumber));

        if (opener.targetScript != null)
        {
            if (GUILayout.Button($"Open Script at Line {opener.lineNumber}"))
            {
                OpenScriptAtLine(opener.targetScript, opener.lineNumber);
            }
        }

        if (GUI.changed)
        {
            EditorUtility.SetDirty(opener);
        }
    }

    private void OpenScriptAtLine(MonoScript script, int line)
    {
        if (script != null)
        {
            AssetDatabase.OpenAsset(script, line);
        }
    }
}
