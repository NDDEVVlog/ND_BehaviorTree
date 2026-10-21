using UnityEngine;

namespace ND.Dialogue
{
    /// <summary>The string field names a variable of the database's schema (the editor shows a picker).</summary>
    public class DialogueVariableAttribute : PropertyAttribute { }

    /// <summary>The string field names a quest of the database's schema (the editor shows a picker).</summary>
    public class DialogueQuestAttribute : PropertyAttribute { }

    /// <summary>The string field names an event declared in the database (the editor shows a picker).</summary>
    public class DialogueEventAttribute : PropertyAttribute { }

    /// <summary>The string field names a conversation of the database (the editor shows a picker).</summary>
    public class DialogueConversationAttribute : PropertyAttribute { }

    /// <summary>The string field names an actor of the database (the editor shows a picker).</summary>
    public class DialogueActorAttribute : PropertyAttribute { }

    /// <summary>
    /// A component or asset that is not a <see cref="DialogueDatabase"/> but works with one (e.g. an event listener):
    /// the editor pickers read names from <see cref="Database"/>.
    /// </summary>
    public interface IDialogueDatabaseOwner
    {
        DialogueDatabase Database { get; }
    }
}
