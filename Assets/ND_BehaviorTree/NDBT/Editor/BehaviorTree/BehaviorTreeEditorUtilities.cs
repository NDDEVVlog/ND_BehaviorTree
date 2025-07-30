using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor
{
    public static class BehaviorTreeEditorUtilities
    {
        /// <summary>
        /// Scans a BehaviorTree for nodes with [RequireComponentInRunner] attribute.
        /// It checks if the owner GameObject has these components and adds any that are missing.
        /// </summary>
        /// <param name="owner">The MonoBehaviour script instance that holds the tree.</param>
        /// <param name="tree">The BehaviorTree asset to scan.</param>
        /// <returns>A formatted string message for a HelpBox if components were added, otherwise null.</returns>
        public static string CheckAndEnforceNodeRequirements(MonoBehaviour owner, BehaviorTree tree)
        {
            if (owner == null || tree == null || tree.nodes == null)
            {
                return null; // Not enough info to check.
            }

            // Use a HashSet to gather unique required component types.
            var requiredComponentTypes = new HashSet<System.Type>();

            foreach (var node in tree.nodes)
            {
                if (node == null) continue;

                // Get all attributes of our custom type from the node's class.
                var attributes = node.GetType().GetCustomAttributes(typeof(RequireComponentInRunnerAttribute), true);
                foreach (RequireComponentInRunnerAttribute attr in attributes)
                {
                    if (attr.RequiredComponentType != null)
                    {
                        requiredComponentTypes.Add(attr.RequiredComponentType);
                    }
                }
            }

            if (requiredComponentTypes.Count == 0)
            {
                return null; // No requirements found.
            }

            var newlyAddedComponents = new List<string>();

            // Now, check the GameObject for each required component.
            foreach (var componentType in requiredComponentTypes)
            {
                if (owner.gameObject.GetComponent(componentType) == null)
                {
                    // The component is missing, so we add it.
                    Undo.AddComponent(owner.gameObject, componentType); // Use Undo for better editor integration
                    newlyAddedComponents.Add(componentType.Name);
                }
            }

            // If we added any components, create a helpful message.
            if (newlyAddedComponents.Count > 0)
            {
                string componentList = string.Join(", ", newlyAddedComponents);
                return $"The Behavior Tree requires the following component(s) which have been added automatically: {componentList}.";
            }

            return null; // Nothing was added.
        }
    }
}
