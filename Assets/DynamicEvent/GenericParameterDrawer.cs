// --- START OF FILE GenericParameterDrawer.cs (Simplified) ---
// --- PLACE THIS FILE IN AN "Editor" FOLDER ---

using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using Sirenix.Utilities;

public class GenericParameterDrawer : OdinValueDrawer<GenericParameter>
{
    protected override void DrawPropertyLayout(GUIContent label)
    {
        // First-level null check is still important for robustness.
        if (this.ValueEntry.SmartValue == null)
        {
            this.CallNextDrawer(label);
            return;
        }

        // Get the child properties from Odin's property tree.
        InspectorProperty nameProp = this.Property.Children["parameterName"];
        InspectorProperty constValueProp = this.Property.Children["constantValue"];

        // Second-level null check for when the inspector tree is building.
        if (nameProp == null || constValueProp == null)
        {
            this.CallNextDrawer(label);
            return;
        }
        
        // --- SIMPLIFIED DRAWING LOGIC ---
        SirenixEditorGUI.BeginBox();
        
        // Use the parameter name as the title for a collapsible foldout.
        var parameterName = nameProp.ValueEntry.WeakSmartValue as string;
        if (!string.IsNullOrEmpty(parameterName))
        {
            // Tying the foldout state to the property ensures it's remembered.
            constValueProp.State.Expanded = SirenixEditorGUI.Foldout(constValueProp.State.Expanded, parameterName.SplitPascalCase());
        }
        
        // If the foldout is expanded, draw the 'constantValue' property.
        // Odin will automatically draw the correct fields for a float, string, or complex struct like DamageInfo.
        if (SirenixEditorGUI.BeginFadeGroup(this, constValueProp.State.Expanded))
        {
            constValueProp.Draw();
        }
        SirenixEditorGUI.EndFadeGroup();
        
        SirenixEditorGUI.EndBox();
    }
}