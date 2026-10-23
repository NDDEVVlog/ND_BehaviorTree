using ND.Editor;
using UnityEditor;

namespace ND.Dialogue.Editor
{
    /// <summary>
    /// Optional integrations with other plugins. Each compiles only while its define is on, and the define is only on while
    /// the other plugin's assembly is in the project (Project Settings → ND Dialogue → Integrations shows them).
    /// </summary>
    [InitializeOnLoad]
    public static class DialogueIntegrations
    {
        /// <summary>ND Behavior Tree: blackboard world, Bark node (Integrations/NDBT). On by itself when the BT plugin is in the project.</summary>
        public static readonly OptionalIntegration Ndbt = new OptionalIntegration("ND Behavior Tree", "ND_DIALOGUE_NDBT", "ND_BehaviorTree.Runtime", automatic: true);

        /// <summary>FMOD for Unity (2.02+): line voices as FMOD events (Integrations/FMOD, assembly ND.Dialogue.FMOD).</summary>
        public static readonly OptionalIntegration Fmod = new OptionalIntegration("FMOD", "ND_DIALOGUE_FMOD", "FMODUnity");

        static DialogueIntegrations() { } // registers the integrations above on load (their watcher needs them listed)
    }
}
