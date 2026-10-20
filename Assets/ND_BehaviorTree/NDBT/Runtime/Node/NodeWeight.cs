namespace ND_BehaviorTree
{
    /// <summary>How a weighted composite uses its children's weights (the editor labels edges accordingly).</summary>
    public enum WeightUsage
    {
        /// <summary>Weighted random pick: edges show each child's chance.</summary>
        Chance,
        /// <summary>Highest score wins: edges show the weight (live: the current score).</summary>
        Score,
    }

    /// <summary>A composite that reads <see cref="Node.weight"/> of its children. Every other parent ignores weights.</summary>
    public interface IWeightedComposite
    {
        WeightUsage WeightUsage { get; }
    }

    /// <summary>An attached decorator that changes its node's weight at runtime (e.g. from a blackboard key).</summary>
    public interface IWeightModifier
    {
        float ModifyWeight(float weight);

        /// <summary>Short text for the editor badge, e.g. "×Menace".</summary>
        string Describe();
    }

    public static class NodeWeight
    {
        /// <summary>
        /// Effective weight of a runtime child entry. The entry may be the outermost attached decorator
        /// (parent → decorators[0] → … → node): the base is the real node's weight, then every attached
        /// <see cref="IWeightModifier"/> is applied, outermost first.
        /// </summary>
        public static float Evaluate(Node entry)
        {
            if (entry == null) return 0f;
            Node terminal = entry;
            for (int guard = 0; guard < 64 && terminal is DecoratorNode d && d.child != null; guard++) terminal = d.child;

            // Only an ATTACHED chain belongs to the terminal node; a legacy standalone decorator keeps its own weight.
            bool attachedChain = terminal != entry && entry is DecoratorNode entryDecorator && terminal.decorators.Contains(entryDecorator);
            if (!attachedChain) return entry is IWeightModifier m ? m.ModifyWeight(entry.weight) : entry.weight;

            float weight = terminal.weight;
            var decorators = terminal.decorators;
            for (int i = 0; i < decorators.Count; i++)
                if (decorators[i] is IWeightModifier modifier) weight = modifier.ModifyWeight(weight);
            return weight;
        }
    }
}
