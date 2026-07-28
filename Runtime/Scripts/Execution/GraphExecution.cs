/*
 *	Created by:  Peter @sHTiF Stefcek
 */

using System.Collections.Generic;

namespace Dash
{
    /// <summary>
    /// Owns the per-run state of a single graph execution — one flow entering the graph — so that
    /// flow can be stopped on its own, without disturbing concurrent executions of the same shared
    /// node objects.
    ///
    /// Carries: identity (<see cref="id"/>), an in-flight frame map (how many open frames each node
    /// has for THIS flow), and the tweens this flow scheduled. <see cref="Stop"/> uses those to tear
    /// the flow down. The frame map and tween list are maintained in parallel with the node-level
    /// ExecutionCount and per-node _activeTweens lists that whole-graph stop still owns.
    ///
    /// Handed to callers by DashGraph.ExecuteGraphInput(..., out execution); a StopNode in FLOW mode
    /// reaches its own execution through the flow data. Not yet added: an id->execution registry and
    /// a natural-completion signal (both want a flow-entry bracket that does not exist yet).
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

        // Set once by Stop(). A stopped execution accepts no new frames and does not propagate;
        // OnExecuteOutput / OnExecuteEnd check this and bail. One-way latch — a fresh flow gets a
        // fresh GraphExecution, so there is nothing to reset.
        public bool IsStopped { get; private set; }

        // Tweens this execution has in flight, across every node it is running. Phase 3 tracks
        // these in PARALLEL with the existing per-node _activeTweens lists (which whole-graph
        // Stop still uses); this list is what a per-execution stop will kill in Phase 4. Lazily
        // allocated.
        private List<DashTween> _tweens;

        public GraphExecution(ExecutionId p_id, DashGraph p_graph)
        {
            id = p_id;
            graph = p_graph;
        }

        /// <summary>Opens a frame for p_node on this execution. Mirrors NodeBase.ExecutionCount++.</summary>
        public void EnterNode(NodeBase p_node)
        {
            if (p_node == null || IsStopped)
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

        /// <summary>Registers a tween as belonging to this execution. Returns it for chaining.</summary>
        public DashTween TrackTween(DashTween p_tween)
        {
            if (p_tween == null)
                return null;

            if (_tweens == null)
                _tweens = new List<DashTween>();

            _tweens.Add(p_tween);
            return p_tween;
        }

        /// <summary>Drops a tween from this execution's list (call when it completes naturally).</summary>
        public void UntrackTween(DashTween p_tween)
        {
            if (_tweens != null && p_tween != null)
                _tweens.Remove(p_tween);
        }

        /// <summary>
        /// Kills every tween this execution has in flight. Uses Kill(false), which runs Clean()
        /// without firing OnComplete, so the downstream flow does NOT resume — this is teardown,
        /// not completion. The matching per-node _activeTweens entries are cleared by the node's
        /// own Stop_Internal during a whole-graph stop; a per-execution stop (Phase 4) will call
        /// this instead.
        /// </summary>
        public void KillTweens()
        {
            if (_tweens == null)
                return;

            // Snapshot count; Kill(false) does not fire the node callback that would Untrack, but
            // iterate defensively and clear at the end regardless.
            for (int i = 0; i < _tweens.Count; i++)
                _tweens[i]?.Kill(false);

            _tweens.Clear();
        }

        public int TweenCount => _tweens == null ? 0 : _tweens.Count;

        /// <summary>
        /// Tears this execution down: kills its in-flight tweens (Kill(false), so none resume),
        /// releases every open node frame so node-level ExecutionCount / IsExecuting stay honest,
        /// and latches IsStopped so nothing downstream keeps running. Idempotent.
        ///
        /// This is the per-flow counterpart to whole-graph DashGraph.Stop(): it touches only the
        /// nodes and tweens THIS flow owns, leaving concurrent executions of the same nodes alone.
        /// </summary>
        public void Stop()
        {
            if (IsStopped)
                return;

            IsStopped = true;

            KillTweens();

            if (_activeFrames != null)
            {
                foreach (var pair in _activeFrames)
                    pair.Key.ReleaseExecutionFrames(pair.Value);

                _activeFrames.Clear();
            }

            TotalFrames = 0;
        }

        public override string ToString()
        {
            return id + " on " + (graph == null ? "<null>" : graph.name);
        }
    }
}
