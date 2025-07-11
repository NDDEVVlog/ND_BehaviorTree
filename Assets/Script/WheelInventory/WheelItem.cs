// WheelItem.cs
using UnityEngine;

[System.Serializable]
public class WheelItem
{
    public string itemName = "New Item";
    public Sprite itemIcon;
    [Range(0.01f, 1f)]
    public float percentageOccupied = 0.1f;
}