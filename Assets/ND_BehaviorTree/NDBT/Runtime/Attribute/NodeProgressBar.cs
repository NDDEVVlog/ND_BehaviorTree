// --- START OF FILE NodeProgressBar.cs ---
using System;
using UnityEngine;

namespace ND_BehaviorTree
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
    public class NodeProgressBar : Attribute
    {
        // --- For hard-coded float values ---
        public float startValue { get; }
        public float endValue { get; }

        // --- For dynamic blackboard variable names ---
        public string startVariable { get; }
        public string endVariable { get; }

        // The variable/field to observe for the current value
        public string variableObserver { get; }

        // --- NEW: For observing fields directly on the node ---
        public string currentValueField { get; }
        public string maxValueField { get; }
        public float fieldStartValue { get; }


        /// <summary>
        /// CONSTRUCTOR 1: Progress bar with static start and end values, watching a blackboard key.
        /// </summary>
        public NodeProgressBar(float startValue, float endValue, string blackboardObserver)
        {
            this.startValue = startValue;
            this.endValue = endValue;
            this.variableObserver = blackboardObserver;
        }

        /// <summary>
        /// CONSTRUCTOR 2: Progress bar with dynamic start/end values from blackboard keys.
        /// </summary>
        public NodeProgressBar(string startBlackboardKey, string endBlackboardKey, string blackboardObserver)
        {
            this.startVariable = startBlackboardKey;
            this.endVariable = endBlackboardKey;
            this.variableObserver = blackboardObserver;
        }

        /// <summary>
        /// CONSTRUCTOR 3 (NEW): Progress bar that reads values directly from fields on the node class.
        /// </summary>
        /// <param name="currentValueField">The name of the field holding the current value (e.g., "elapsedTime").</param>
        /// <param name="maxValueField">The name of the field holding the max value (e.g., "timeLimit").</param>
        /// <param name="startValue">A fixed float for the start value, defaults to 0.</param>
        public NodeProgressBar(string currentValueField, string maxValueField, float startValue = 0f)
        {
            this.currentValueField = currentValueField;
            this.maxValueField = maxValueField;
            this.fieldStartValue = startValue;
        }
    }
}
// --- END OF FILE NodeProgressBar.cs ---