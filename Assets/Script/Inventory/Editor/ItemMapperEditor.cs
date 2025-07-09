using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector.Editor;
using ND.Inventory;

[CustomEditor(typeof(ItemMapper))]
public class ItemMapperEditor : OdinEditor
{
    private ItemMapper targetItemMapper;



    public override void OnInspectorGUI()
    {
        if (targetItemMapper == null)
        {
            EditorGUILayout.HelpBox("Target is not of type ItemMapper.", MessageType.Error);
            return;
        }

        serializedObject.Update();

        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox("Edit item properties here.", MessageType.Info);
        EditorGUILayout.Space(10);

        // Draw the default inspector without the ObjectReference fields
        DrawPropertiesExcluding(serializedObject, "DropObject", "PackDropObject");

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Drop Object", EditorStyles.boldLabel);
        targetItemMapper.DropObject.GUID = EditorGUILayout.TextField("GUID", targetItemMapper.DropObject.GUID);
        targetItemMapper.DropObject.Object = (GameObject)EditorGUILayout.ObjectField("Object", targetItemMapper.DropObject.Object, typeof(GameObject), false);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Pack Drop Object", EditorStyles.boldLabel);
        targetItemMapper.PackDropObject.GUID = EditorGUILayout.TextField("GUID", targetItemMapper.PackDropObject.GUID);
        targetItemMapper.PackDropObject.Object = (GameObject)EditorGUILayout.ObjectField("Object", targetItemMapper.PackDropObject.Object, typeof(GameObject), false);

        serializedObject.ApplyModifiedProperties();
    }
}
