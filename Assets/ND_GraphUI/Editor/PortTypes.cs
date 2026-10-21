namespace ND.GraphUI
{
    public enum PortDirection { Input, Output }

    /// <summary>Single: one edge at most. Multi: any number (drawn wider along the node edge).</summary>
    public enum PortCapacity { Single, Multi }

    /// <summary>
    /// Which node sides the ports sit on, and so which way edges leave them.
    /// Vertical: input on top, output at the bottom (trees). Horizontal: input left, output right (flows, dialogue).
    /// </summary>
    public enum PortOrientation { Vertical, Horizontal }
}
