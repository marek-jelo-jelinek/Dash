/*
 *	Created by:  Peter @sHTiF Stefcek
 */

namespace Dash
{
    public enum StopMode
    {
        NONE,
        GRAPH,
        CONNECTED,
        // Appended at the end: this enum is serialized by ordinal, so existing GRAPH/CONNECTED
        // values in saved graphs must keep their indices. FLOW stops only the current flow.
        FLOW,
    }
}