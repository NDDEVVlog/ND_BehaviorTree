using UnityEngine;

namespace ND_BehaviorTree.Story
{
    [StorySentence("Set {variable} to {text}")]
    [NodeDescription("Writes a text into a String key (e.g. a mood a Switch reads later).",
        "Bind 'variable' with @ to a String key. The text can be typed inline or read from another String key. Fails (logged once) when 'variable' is not bound.")]
    [NodeInfo("Set Text", "Story/Set Text", true, false, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/SetValue.png")]
    public class SetTextNode : StoryActionNode
    {
        [BlackboardKeyType(typeof(string))] public Key variableKey;
        public string text = "Happy";
        [BlackboardKeyType(typeof(string))] public Key textKey;

        protected override Status OnProcess()
        {
            if (variableKey == null || blackboard == null)
            {
                LogKeyError("bind 'variable' to a String key.");
                return Status.Failure;
            }
            if (blackboard.SetValue(variableKey.keyName, Read(textKey, text))) return Status.Success;
            LogKeyError($"key '{variableKey.keyName}' is missing from the runtime blackboard or is not a String key.");
            return Status.Failure;
        }
    }
}
