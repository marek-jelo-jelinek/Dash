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

        // Tweens this execution has in flight, with the node that scheduled each one. Owner is
        // needed so a per-flow kill can also prune the owning node's _activeTweens list — killed
        // tweens return to DashTween's pool and get reused, so a stale node-list entry would let a
        // later node-scoped stop kill an unrelated flow's recycled tween. Lazily allocated.
        private struct TrackedTween
        {
            public NodeBase owner;
            public DashTween tween;
        }

        private List<TrackedTween> _tweens;

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

        /// <summary>Registers a tween as belonging to this execution, owned by p_owner. Returns it for chaining.</summary>
        public DashTween TrackTween(NodeBase p_owner, DashTween p_tween)
        {
            if (p_tween == null)
                return null;

            if (_tweens == null)
                _tweens = new List<TrackedTween>();

            _tweens.Add(new TrackedTween { owner = p_owner, tween = p_tween });
            return p_tween;
        }

        /// <summary>Drops a tween from this execution's list (call when it completes naturally).</summary>
        public void UntrackTween(DashTween p_tween)
        {
            if (_tweens == null || p_tween == null)
                return;

            for (int i = 0; i < _tweens.Count; i++)
            {
                if (_tweens[i].tween == p_tween)
                {
                    _tweens.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>
        /// Kills every tween this execution has in flight. Uses Kill(false), which runs Clean()
        /// without firing OnComplete, so the downstream flow does NOT resume — this is teardown,
        /// not completion. Also prunes each tween from its owner node's list so no stale reference
        /// survives to poison a later node-scoped stop after the tween instance is pooled/reused.
        /// </summary>
        public void KillTweens()
        {
            if (_tweens == null)
                return;

            for (int i = 0; i < _tweens.Count; i++)
            {
                TrackedTween tracked = _tweens[i];
                tracked.tween?.Kill(false);
                tracked.owner?.RemoveActiveTween(tracked.tween);
            }

            _tweens.Clear();
        }

        /// <summary>
        /// Kills only the tweens p_node scheduled on this execution, leaving the rest of the flow
        /// running. Used by killOnNullEncounter: the animated target died, so this node's animation
        /// for THIS flow stops — without touching other flows on the node or other nodes of the flow.
        /// </summary>
        public void KillTweens(NodeBase p_node)
        {
            if (_tweens == null || p_node == null)
                return;

            for (int i = _tweens.Count - 1; i >= 0; i--)
            {
                TrackedTween tracked = _tweens[i];
                if (tracked.owner != p_node)
                    continue;

                tracked.tween?.Kill(false);
                p_node.RemoveActiveTween(tracked.tween);
                _tweens.RemoveAt(i);
            }
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
