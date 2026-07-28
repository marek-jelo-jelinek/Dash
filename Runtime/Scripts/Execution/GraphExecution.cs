/*
 *	Created by:  Peter @sHTiF Stefcek
 */

namespace Dash
{
    /// <summary>
    /// Owns the per-run state of a single graph execution — one flow entering the graph.
    ///
    /// Phase 1 carries identity only. Later phases move per-execution state that currently lives
    /// on shared node objects (in-flight frame counts, active tweens, an error flag) onto this
    /// object, plus a disposal list, so a stop can tear down exactly what one execution created
    /// without touching concurrent executions of the same nodes.
    /// </summary>
    public class GraphExecution
    {
        public readonly ExecutionId id;
        public readonly DashGraph graph;

        public GraphExecution(ExecutionId p_id, DashGraph p_graph)
        {
            id = p_id;
            graph = p_graph;
        }

        public override string ToString()
        {
            return id + " on " + (graph == null ? "<null>" : graph.name);
        }
    }
}
