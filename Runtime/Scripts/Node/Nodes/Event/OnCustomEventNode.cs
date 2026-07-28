/*
 *	Created by:  Peter @sHTiF Stefcek
 */

using System;
using System.ComponentModel;
using Dash.Attributes;
using Dash.Editor;
using OdinSerializer.Utilities;
using UnityEditor;
using UnityEngine;

namespace Dash
{
    [Attributes.Tooltip("Executes on custom event callback.")]
    [Attributes.Category(NodeCategoryType.EVENT)]
    [OutputCount(1)]
    public class OnCustomEventNode : NodeBase<OnCustomEventNodeModel>
    {
        internal override void Initialize()
        {
            _graph.AddListener(Model.eventName, Execute, Model.priority);
        }

        override protected void OnExecuteStart(NodeFlowData p_flowData)
        {
            if (Model.useSequencer)
            {
                string sequencerId = GetParameterValue(Model.sequencerId, p_flowData);
                int sequencerPriority = GetParameterValue(Model.sequencerPriority, p_flowData);
                if (!sequencerId.IsNullOrWhitespace())
                {
                    EventSequencer sequencer = DashCore.Instance.GetOrCreateSequencer(sequencerId);
                    string eventName = Model.eventName;

                    Action callback = () =>
                    {
                        OnExecuteEnd(p_flowData);
                        OnExecuteOutput(0, p_flowData);
                    };

                    // Teardown for the sequencer claim: a stopped flow must free it or every later
                    // event on this sequencer queues forever. If the flow is still WAITING its
                    // queue entry is cancelled outright; if it already runs the slot, EndEvent
                    // frees it and advances the queue. Unregistered by EndEventNode when the flow
                    // releases the slot naturally.
                    p_flowData.execution?.RegisterDisposable(
                        GraphExecution.GetSequencerDisposableKey(sequencerId, eventName), () =>
                        {
                            if (!sequencer.CancelEvent(eventName, callback))
                                sequencer.EndEvent(eventName);
                        });

                    sequencer.StartEvent(eventName, sequencerPriority, callback);
                    return;
                }
            }
            
            //Debug.Log("OnCustomEvent: "+Model.eventName);
            OnExecuteEnd(p_flowData);
            OnExecuteOutput(0, p_flowData);
        }

#if UNITY_EDITOR
        public override Vector2 Size => new Vector2(DashEditorCore.Skin.GetStyle("NodeTitle").CalcSize(new GUIContent(Name)).x + 35, 85);
        
        public override string CustomName => "Event " + Model.eventName;

        protected override void DrawCustomGUI(Rect p_rect)
        {
            Rect offsetRect = new Rect(rect.x + _graph.viewOffset.x, rect.y + _graph.viewOffset.y, rect.width, rect.height);
            
            GUI.Label(new Rect(new Vector2(offsetRect.x + offsetRect.width*.5f-50, offsetRect.y+offsetRect.height/2), new Vector2(100, 20)), Model.eventName , DashEditorCore.Skin.GetStyle("NodeText"));
        }
#endif
    }
}
