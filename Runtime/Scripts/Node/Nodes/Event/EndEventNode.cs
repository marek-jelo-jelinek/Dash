/*
 *	Created by:  Peter @sHTiF Stefcek
 */

using Dash.Attributes;
using OdinSerializer.Utilities;

namespace Dash
{
    [Tooltip("End an event in sequencer.")]
    [Category(NodeCategoryType.EVENT)]
    [InputCount(1)]
    [OutputCount(1)]
    [Size(170,85)]
    public class EndEventNode : NodeBase<EndEventNodeModel>
    {
        protected override void OnExecuteStart(NodeFlowData p_flowData)
        {
            string eventName = GetParameterValue(Model.eventName, p_flowData);
            string sequencerId = GetParameterValue(Model.sequencerId, p_flowData);

            if (!sequencerId.IsNullOrWhitespace())
            {
                DashCore.Instance.GetOrCreateSequencer(sequencerId).EndEvent(eventName);

                // The slot is released; drop the teardown registered by OnCustomEventNode so a
                // later Stop of this flow cannot EndEvent a slot it no longer holds.
                p_flowData.execution?.UnregisterDisposable(
                    GraphExecution.GetSequencerDisposableKey(sequencerId, eventName));
            }

            OnExecuteEnd(p_flowData);
            OnExecuteOutput(0, p_flowData);
        }
    }
}