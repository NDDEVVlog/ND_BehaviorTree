using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;

// ENUM
public enum MagicType { Shape, Stats, Spell }
public enum CapacitySize { S = 1, S2 = 2, M = 4, L = 6 }

// BASE CLASS
public abstract class SOMagicGear : ScriptableObject
{
    public string GearID;
    [ReadOnly] public MagicType Type;
    public Sprite icon;
    public CapacitySize Size = CapacitySize.S;
    public float CDTime;
    public int CapacityCost => (int)Size;
    
    public virtual void DecodeGear(){}
}

// INTERFACES
public interface IGearShape
{
    //void ExecuteRecipe(List<SOMagicGear> allGearsInSlot, SOMagicGear_Shape owner);
}
public interface IGearStat
{
    string StatID { get; }
}
public interface IGearSpell
{
    string SpellID { get; }
}


