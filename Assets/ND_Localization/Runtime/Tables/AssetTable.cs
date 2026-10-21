using UnityEngine;

namespace ND.Localization
{
    /// <summary>Per-locale Unity objects: sprites, audio clips, textures, prefabs…</summary>
    [CreateAssetMenu(fileName = "AssetTable", menuName = "ND Localization/Asset Table", order = 11)]
    public class AssetTable : LocalizationTable<Object> { }
}
