using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND.Inventory
{   
    public enum ItemType
    {
        Heal,
        Normal,
        EnviromentInteract,
        ItemPart,
        Weapon,
        Bullet,
    }
    public enum ItemAction
    {
        None,
        Increase,
        Decrease,
        ItemValue,
    }
    public enum ItemAnchorFrame
    {
        Center,
        Top,
        Bot,
    }
}