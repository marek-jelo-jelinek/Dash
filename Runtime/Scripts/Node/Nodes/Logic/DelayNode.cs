/*
 *	Created by:  Peter @sHTiF Stefcek
 */

using System;
using System.Collections.Generic;
using Dash.Attributes;
using Dash.Editor;
using UnityEngine;

namespace Dash
{
    [Category(NodeCategoryType.LOGIC)]
    [OutputCount(1)]
    [InputCount(1)]
    public class DelayNode : NodeBase<DelayNodeModel>
    {
        override protected void OnExecuteStart(NodeFlowData p_flowData)
        {
            float time = GetParameterValue(Model.time, p_flowData);

            if (time == 0)
            {
                OnExecuteEnd(p_flowData);
                OnExecuteOutput(0, p_flowData);
            }
            else
            {
                DashTween tween = DashTween.To(Graph.Controller, 0, 1, time);

                tween.OnComplete(() =>
                {
                    OnExecuteEnd(p_flowData);
                    OnExecuteOutput(0, p_flowData);
                    UntrackTween(tween, p_flowData);
                });

                TrackTween(tween, p_flowData);
                tween.Start();
            }
        }

        public override bool IsSynchronous()
        {
            return !Model.time.isExpression && Model.time.GetValue(null) == 0;
        }
        
        #if UNITY_EDITOR
        protected override void DrawCustomGUI(Rect p_rect)
        {
            GUI.Label(new Rect(p_rect.x + p_rect.width / 2 - 50, p_rect.y + p_rect.height - 32, 100, 20),
                "Time: " + Model.time.ToString() + "s", DashEditorCore.Skin.GetStyle("NodeText"));
        }
        #endif
    }
}