// FILE: GOAP/IGoapPrecondition.cs

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND_BehaviorTree.GOAP
{
    /// <summary>
    /// Interface for any condition that can be evaluated by the GOAP planner.
    /// This allows for creating complex, behavior-driven preconditions beyond simple state checks.
    /// </summary>
    public interface IGoapPrecondition
    {
        /// <summary>
        /// Checks if the precondition is met given the current world state and agent context.
        /// </summary>
        /// <param name="agent">The GameObject running the behavior tree.</param>
        /// <param name="worldState">The current state of the world from the blackboard.</param>
        /// <returns>True if the condition is met, otherwise false.</returns>
        bool IsMet(GameObject agent, Blackboard blackboard , Dictionary<string, object> worldState);

        /// <summary>
        /// Provides a human-readable description for the editor.
        /// </summary>
        string GetDescription();
    }
}