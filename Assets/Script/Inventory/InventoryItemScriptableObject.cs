using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ND.Inventory {
    [CreateAssetMenu]
    public class InventoryItemScriptableObject : ScriptableObject
    {
        public List<ItemMapper> itemMappers = new List<ItemMapper>();
    }
}
