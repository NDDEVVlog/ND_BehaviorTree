using UnityEngine;

namespace ND.Localization
{
    /// <summary>Per-locale colors, for when a color carries a cultural meaning (e.g. red = luck vs. red = danger).</summary>
    [CreateAssetMenu(fileName = "ColorTable", menuName = "ND Localization/Color Table", order = 12)]
    public class ColorTable : LocalizationTable<Color> { }
}
