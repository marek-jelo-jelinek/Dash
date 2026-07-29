/*
 *	Created by:  Peter @sHTiF Stefcek
 */

namespace Dash
{
    public class SendCustomEventNodeModel : NodeModelBase
    {
        public Parameter<string> eventName = new Parameter<string>("");

        public Parameter<bool> global = new Parameter<bool>(false);

        public Parameter<bool> sendData = new Parameter<bool>(true);

        // When true the event is sent WITHOUT this flow's execution identity: every receiving
        // graph mints its own run (origin EVENT <name>), individually addressable/stoppable via
        // StopExecutionsByEvent — and stopping the sender's flow no longer stops those runs.
        // When false (default) the triggered cascade belongs to the sender's execution.
        public Parameter<bool> detachExecution = new Parameter<bool>(false);
    }
}