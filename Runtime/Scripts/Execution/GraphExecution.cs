/*
 *	Created by:  Peter @sHTiF Stefcek
 */

using System.Collections.Generic;

namespace Dash
{
    /// <summary>
    /// Owns the per-run state of a single graph execution — one flow entering the graph.
    ///
    /// Phase 1 carried identity only. Phase 2 adds the in-flight frame map: how many times each
    /// node currently has an open execution frame belonging to THIS execution. That is the piece
    /// that lets a later per-execution stop tear down exactly the nodes one flow is running,
    /// without touching concurrent executions of the same shared node objects.
    ///
    /// Later phases move the remaining per-execution state (active tweens, the error flag) and a
    /// disposal list onto this object, and add a completion signal once every flow entry point is
    /// bracketed (that bracket arrives with the Phase 3 tween consolidation, so completion firing
    /// is intentionally not wired here yet).
    /// </summary>
    public class GraphExecution
    {
        public readonly ExecutionId id;
        public readonly DashGraph graph;

        // node -> number of open frames this execution currently has on that node. Entries are
        // removed when they hit zero, so ContainsKey answers "is this node running for me". Lazily
        // allocated; most executions touch only a handful of nodes.
        private Dictionary<NodeBase, int> _activeFrames;

        // Sum of _activeFrames values. Kept incrementally so callers do not have to walk the map.
        public int TotalFrames { get; private set; }

        public GraphExecution(ExecutionId p_id, DashGraph p_graph)
        {
            id = p_id;
            graph = p_graph;
        }

        /// <summary>Opens a frame for p_node on this execution. Mirrors NodeBase.ExecutionCount++.</summary>
        public void EnterNode(NodeBase p_node)
        {
            if (p_node == null)
                return;

            if (_activeFrames == null)
                _activeFrames = new Dictionary<NodeBase, int>();

            int count;
            _activeFrames.TryGetValue(p_node, out count);
            _activeFrames[p_node] = count + 1;
            TotalFrames++;
        }

        /// <summary>Closes a frame for p_node on this execution. Mirrors NodeBase.ExecutionCount--.</summary>
        public void ExitNode(NodeBase p_node)
        {
            if (p_node == null || _activeFrames == null)
                return;

            int count;
            if (!_activeFrames.TryGetValue(p_node, out count))
                return;

            if (count <= 1)
                _activeFrames.Remove(p_node);
            else
                _activeFrames[p_node] = count - 1;

            TotalFrames--;
        }

        /// <summary>How many frames p_node currently has open on this execution.</summary>
        public int FrameCount(NodeBase p_node)
        {
            int count;
            if (_activeFrames != null && _activeFrames.TryGetValue(p_node, out count))
                return count;

            return 0;
        }

        /// <summary>True while p_node has at least one open frame on this execution.</summary>
        public bool IsNodeActive(NodeBase p_node)
        {
            return _activeFrames != null && _activeFrames.ContainsKey(p_node);
        }

        public override string ToString()
        {
            return id + " on " + (graph == null ? "<null>" : graph.name);
        }
    }
}
