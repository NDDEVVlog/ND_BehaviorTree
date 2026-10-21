using System;
using UnityEngine;

namespace ND.Localization
{
    /// <summary>
    /// Text that starts as plain writing and can be connected to a <see cref="StringTable"/> later.
    /// Not connected (<see cref="IsLocalized"/> false): <see cref="GetText"/> returns <see cref="Text"/>.
    /// Connected: it behaves as a <see cref="LocalizedString"/> and the plain text is only a note for writers.
    /// Derives from <see cref="LocalizedString"/>, so a field changed from LocalizedString keeps its table link.
    /// </summary>
    [Serializable]
    public class LocalizableString : LocalizedString
    {
        [SerializeField, TextArea(1, 6)] private string m_Text = string.Empty;

        /// <summary>The plain text (used while not connected to a table).</summary>
        public string Text
        {
            get => m_Text;
            set => m_Text = value ?? string.Empty;
        }

        public bool IsLocalized => !IsEmpty;

        /// <summary>True when there is something to show: a table link or non-empty plain text.</summary>
        public bool HasContent => IsLocalized || !string.IsNullOrEmpty(m_Text);

        /// <summary>The text to show: the translation when connected, else the plain text (both formatted with <see cref="LocalizedString.Arguments"/>).</summary>
        public string GetText() => IsLocalized ? Resolve() : LocalizationService.Format(m_Text, Arguments);

        /// <summary>Disconnects from the table and goes back to plain text (the table entry is left in place).</summary>
        public void Unlink(string plainText)
        {
            m_Text = plainText ?? string.Empty;
            SetReference(null, LocalizationTable.InvalidId);
        }

        public override string ToString() => IsLocalized ? base.ToString() : $"\"{m_Text}\"";
    }
}
