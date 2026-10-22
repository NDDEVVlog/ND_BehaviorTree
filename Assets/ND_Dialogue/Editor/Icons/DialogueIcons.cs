using ND.Localization.Editor;
using UnityEditor;

namespace ND.Dialogue.Editor
{
    /// <summary>Gives Dialogue and Character Database assets their own icons ("&lt;Type&gt; Icon.png" next to this file).</summary>
    [InitializeOnLoad]
    public static class DialogueIcons
    {
        static DialogueIcons() => EditorApplication.delayCall += () => ScriptIcons.Apply(new[] { typeof(DialogueDatabase), typeof(CharacterDatabase) });
    }
}
