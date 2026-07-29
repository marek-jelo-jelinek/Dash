/*
 *	Created by:  Peter @sHTiF Stefcek
 */

using Dash.Attributes;
using Dash.Editor;
using OdinSerializer.Utilities;
using UnityEditor;
using UnityEngine;

namespace Dash
{
    [Attributes.Tooltip("Send a custom event.")]
    [Category(NodeCategoryType.EVENT)]
    [InputCount(1)]
    [OutputCount(1)]
    [Size(170,85)]
    public class SendCustomEventNode : NodeBase<SendCustomEventNodeModel>
    {
        override protected void OnExecuteStart(NodeFlowData p_flowData)
        {
            string eventName = GetParameterValue(Model.eventName, p_flowData);
            bool global = GetParameterValue(Model.global, p_flowData);
            bool sendData = GetParameterValue(Model.sendData, p_flowData);
            bool detach = GetParameterValue(Model.detachExecution, p_flowData);

            // Identity: by default the triggered cascade rides the sender's execution — sendData
            // controls data, not identity — so a targeted stop of the sender tears the cascade
            // down too. With detachExecution the event is sent WITHOUT identity: each receiving
            // graph mints its own run (origin EVENT <name>), stoppable per graph and unaffected
            // by stopping the sender. Detach must never strip the sender's own flow data, hence
            // the clone when forwarding data.
            NodeFlowData eventData = sendData ? (detach ? p_flowData.Clone() : p_flowData) : NodeFlowDataFactory.Create();
            if (detach)
                eventData.execution = null;
            else if (!sendData)
                eventData.execution = p_flowData.execution;

            if (global)
            {
                #if UNITY_EDITOR
                if (DashEditorCore.Previewer.IsPreviewing)
                {
                    _graph.SendEvent(eventName, eventData);
                }
                else
                {
                    DashCore.Instance.SendEvent(eventName, eventData);
                }
                #else
                DashCore.Instance.SendEvent(eventName, eventData);
                #endif
            }
            else
            {
                _graph.SendEvent(eventName, eventData);
            }

            OnExecuteEnd(p_flowData);
            OnExecuteOutput(0, p_flowData);
        }

        #region EDITOR_CODE
#if UNITY_EDITOR
        protected override void DrawCustomGUI(Rect p_rect)
        {
            Rect offsetRect = new Rect(rect.x + _graph.viewOffset.x, rect.y + _graph.viewOffset.y, rect.width, rect.height);

            // Need to do this check for older versions
            if (Model.eventName == null)
                return;
            
            if (!Model.eventName.isExpression)
            {
                GUI.Label(
                    new Rect(
                        new Vector2(offsetRect.x + offsetRect.width * .5f - 50, offsetRect.y + offsetRect.height / 2),
                        new Vector2(100, 20)), Model.eventName.GetValue(null), DashEditorCore.Skin.GetStyle("NodeText"));
            }
            else
            {
                GUI.Label(
                    new Rect(
                        new Vector2(offsetRect.x + offsetRect.width * .5f - 50, offsetRect.y + offsetRect.height / 2),
                        new Vector2(100, 20)), "[EXP]", DashEditorCore.Skin.GetStyle("NodeText"));
            }
        }
#endif
        #endregion
    }
}