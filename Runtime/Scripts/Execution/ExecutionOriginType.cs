/*
 *	Created by:  Peter @sHTiF Stefcek
 */

namespace Dash
{
    /// <summary>
    /// How a GraphExecution entered its graph. NONE covers flows minted by the NodeBase.Execute
    /// safety net (hand-built flow data, direct node execution).
    /// </summary>
    public enum ExecutionOriginType
    {
        NONE,
        INPUT,
        EVENT,
    }
}
