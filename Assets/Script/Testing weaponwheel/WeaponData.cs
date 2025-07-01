using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class WeaponData
{
    public string name;
    public Sprite icon; // Optional, if you want icons on wedges
    public Color baseColor = Color.gray;
    public Color hoverColor = Color.yellow;
    public Color selectedColor = Color.green;
    [Range(0.1f, 5f)]
    public float angleWeight = 1f; // Relative size of the slice

    // Runtime calculated values
    [HideInInspector] public float startAngle;
    [HideInInspector] public float sweepAngle;

    public WeaponData(string name, float weight, Color baseCol, Color hovCol)
    {
        this.name = name;
        this.angleWeight = weight;
        this.baseColor = baseCol;
        this.hoverColor = hovCol;
    }
}