/*
 *	Created by:  Peter @sHTiF Stefcek
 */

using System;

namespace Dash
{
    /// <summary>
    /// Identity of a single graph execution (one flow that entered the graph). Carried on
    /// <see cref="NodeFlowData"/> so concurrent flows through the same node can be told apart.
    /// <see cref="None"/> (value 0) means "no execution assigned yet".
    /// </summary>
    public readonly struct ExecutionId : IEquatable<ExecutionId>
    {
        public static readonly ExecutionId None = new ExecutionId(0);

        public readonly int value;

        public ExecutionId(int p_value)
        {
            value = p_value;
        }

        public bool IsValid => value != 0;

        public bool Equals(ExecutionId p_other) => value == p_other.value;

        public override bool Equals(object p_obj) => p_obj is ExecutionId other && Equals(other);

        public override int GetHashCode() => value;

        public override string ToString() => "Execution#" + value;

        public static bool operator ==(ExecutionId p_a, ExecutionId p_b) => p_a.value == p_b.value;

        public static bool operator !=(ExecutionId p_a, ExecutionId p_b) => p_a.value != p_b.value;
    }
}
