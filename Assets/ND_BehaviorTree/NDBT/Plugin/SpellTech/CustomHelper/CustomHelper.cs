using UnityEngine;

namespace SpellTech.SoraExtensions
{
    public static class CustomHelper
    {
        // Directly use `this` to allow the method to be called on MonoBehaviour and related classes
        public static T GetComp<T>(this MonoBehaviour monoBehaviour) where T : Component
        {
            return monoBehaviour.gameObject.GetComp<T>();
        }

        // Same for GameObject
        public static T GetComp<T>(this GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component == null)
            {
                component = gameObject.GetComponentInChildren<T>();
            }

            if (component == null)
            {
                component = gameObject.GetComponentInParent<T>();
            }
            return component;
        }
    }
}