// WheelItem.cs
using UnityEngine;

[System.Serializable]
public class WheelItem
{
    public SOMagicGear gearData;
    [Range(0.01f, 1f)]
    public float percentageOccupied = 0.1f;
}