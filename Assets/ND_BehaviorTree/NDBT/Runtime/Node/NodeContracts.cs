namespace ND_BehaviorTree
{
    /// <summary>
    /// A composite whose children are PART of it rather than independent graph nodes (e.g. a Switch's generated Case
    /// nodes, the actions stacked inside a Block). The editor:
    ///   * keeps their order as authored (never re-sorts them by X position),
    ///   * deletes / copies them together with the owner,
    ///   * does not let the user connect or disconnect them by hand (the owner's editor tools manage them).
    /// Runtime behaviour is unchanged: they are ordinary children.
    /// </summary>
    public interface IOwnedChildren { }

    /// <summary>
    /// Owned children that are drawn INSIDE the owner's node view (rows of a Block) instead of as separate nodes with
    /// edges on the graph.
    /// </summary>
    public interface IEmbeddedChildren : IOwnedChildren { }

    /// <summary>
    /// A composite whose children's list order has no meaning (e.g. a GOAP planner picks among its actions).
    /// The validator does not ask to sort them by position. Children stay ordinary and user-connectable.
    /// </summary>
    public interface IOrderIndependentChildren { }
}
