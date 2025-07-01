using UnityEngine;

// Define the struct. [System.Serializable] is good practice.
[System.Serializable]
public struct DamageInfo
{
    public int damageAmount;
    public string elementType;
    public bool isCriticalHit;
}
[System.Serializable]
public struct MeMayBeoVL
{
    public DamageInfo damageInfo;
    public Color color;
    public Transform quantrongchogi;
}

// This is the main testing script.
public class AnimationEventTester : MonoBehaviour
{
    [Header("Dynamic Value Source")]
    [Tooltip("This value can be read by an event using a dynamic parameter.")]
    public float currentPowerLevel = 75.5f;

    // 1. A function with a single string parameter
    public void PlaySound(string soundName)
    {
        Debug.Log($"<color=cyan>EVENT TRIGGERED: PlaySound</color> -> Received sound name: '{soundName}'");
    }

    // 2. A function with a float and an int parameter
    public void ExecuteAttack(float baseDamage, int comboMultiplier)
    {
        float totalDamage = baseDamage * comboMultiplier;
        Debug.Log($"<color=orange>EVENT TRIGGERED: ExecuteAttack</color> -> Base Damage: {baseDamage}, Multiplier: {comboMultiplier}. Total Damage: {totalDamage}");
    }

    // 3. A function with a struct parameter
    // NOTE: This cannot be set up from the Inspector with our current system.
    // It must be called programmatically, as shown in the setup guide below.
    public void ApplyComplexDamage(DamageInfo info)
    {
        Debug.Log($"<color=red>EVENT TRIGGERED: ApplyComplexDamage</color> -> Damage: {info.damageAmount}, Element: '{info.elementType}', Is Critical: {info.isCriticalHit}");
    }

    // A simple function to test reading a dynamic value
    public void ConsumePower(float powerConsumed)
    {
        Debug.Log($"<color=yellow>EVENT TRIGGERED: ConsumePower</color> -> Power consumed this frame: {powerConsumed}. Current Power Level was {currentPowerLevel}.");
    }

    public void TestMeMayBeo(MeMayBeoVL meMayBeoVL)
    {
        Debug.Log($"<color=purple>EVENT TRIGGERED: ConsumePower</color> -> Color this frame: {meMayBeoVL.color}. Current Power Level was {meMayBeoVL.damageInfo.damageAmount}.");
    }

    // Helper function to show how to call the struct-based event from code
    public void TriggerStructTest()
    {
        Debug.Log("--- Manually triggering the struct-based event for testing ---");

        // 1. Get the hub component
        AnimationEventHub hub = GetComponent<AnimationEventHub>();
        if (hub == null)
        {
            Debug.LogError("AnimationEventHub component not found on this GameObject!");
            return;
        }

        // 2. Create the event data programmatically
        var structEvent = new DynamicEvent
        {
            eventName = "ProgrammaticStructEvent",
            target = this, // Target this script instance
            methodName = "ApplyComplexDamage"
        };

        // 3. Create the struct data and the generic parameter wrapper
        var damageData = new DamageInfo
        {
            damageAmount = 999,
            elementType = "Chaos",
            isCriticalHit = true
        };

        var parameter = new GenericParameter<DamageInfo>
        {
            parameterName = "info",
            constantValue = damageData
        };

        structEvent.genericParameters.Add(parameter);

        // 4. Invoke the event directly
        structEvent.Invoke();
    }
}