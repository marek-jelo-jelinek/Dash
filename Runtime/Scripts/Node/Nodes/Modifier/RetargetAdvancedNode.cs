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
    [Attributes.Tooltip("Changes a current target within NodeFlowData with advanced option.")]
    [Category(NodeCategoryType.MODIFIER, "Modifier/Retarget")]
    [OutputCount(1)]
    [InputCount(1)]
    [Size(160,85)]
    public class RetargetAdvancedNode : NodeBase<RetargetAdvancedNodeModel>
    {
        [NonSerialized]
        protected List<DashTween> _activeTweens;
        
        override protected void OnExecuteStart(NodeFlowData p_flowData)
        {
            if (_activeTweens == null) _activeTweens = new List<DashTween>();
            
            List<Transform> transforms = new List<Transform>();
            Transform transform;
            
            if (!string.IsNullOrEmpty(Model.target))
            {
                if (!p_flowData.HasAttribute("target") && Model.isChild)
                {
                    SetError("Cannot retarget to a child of null");
                    OnExecuteEnd(p_flowData);

                    return;
                }
                
                if (Model.isChild)
                {
                    if (Model.findAll)
                    {
                        transforms = Controller.transform.DeepFindAll(Model.target);
                    }
                    else
                    {
                        transform = Controller.transform.DeepFind(Model.target);
                        if (transform != null) transforms.Add(transform);
                    }
                }
                else
                {
                    if (Model.findAll)
                    {
                        transforms = Controller.transform.root.DeepFindAll(Model.target);
                    }
                    else
                    {
                        transform = Controller.transform.root.DeepFind(Model.target);
                        if (transform != null) transforms.Add(transform);
                    }
                }

                if (transforms.Count == 0)
                {
                    SetError("Zero valid retargets found");
                    OnExecuteEnd(p_flowData);

                    return;
                }

                for (int i = 0; i < transforms.Count; i++)
                {
                    transform = Model.inReverse ? transforms[transforms.Count - i - 1] : transforms[i];
                    NodeFlowData data = p_flowData.Clone();
                    data.SetAttribute("target", transform);

                    if (Model.delay.GetValue(ParameterResolver) == 0)
                    {
                        OnExecuteOutput(0, data);
                    }
                    else
                    {
                        float time = Model.delay.GetValue(ParameterResolver) * i;
                        DashTween tween = DashTween.To(Graph.Controller, 0, 1, time);
                        tween.OnComplete(() =>
                        {
                            _activeTweens.Remove(tween);
                            p_flowData.execution?.UntrackTween(tween);
                            OnExecuteOutput(0, data);
                        });
                        tween.Start();
                        _activeTweens.Add(tween);
                        p_flowData.execution?.TrackTween(tween);
                    }
                }

                if (Model.delay.GetValue(ParameterResolver) == 0)
                {
                    OnExecuteEnd(p_flowData);
                }
                else
                {
                    float time = Model.delay.GetValue(ParameterResolver) * transforms.Count;
                    DashTween tween = DashTween.To(Graph.Controller, 0, 1, time);
                    tween.OnComplete(() =>
                    {
                        _activeTweens.Remove(tween);
                        p_flowData.execution?.UntrackTween(tween);
                        OnExecuteEnd(p_flowData);
                    });
                    tween.Start();
                    _activeTweens.Add(tween);
                    p_flowData.execution?.TrackTween(tween);
                }
            }
            else
            {
                SetError("Zero valid retargets found");
                OnExecuteEnd(p_flowData);
            }
        }

        // Leak fix: this node scheduled delay tweens but had no Stop_Internal, so a whole-graph
        // stop left them running. Mirror the pattern used by the other delay-based nodes.
        protected override void Stop_Internal()
        {
            _activeTweens?.ForEach(t => t.Kill(false));
            _activeTweens = new List<DashTween>();
        }

        public override bool IsSynchronous()
        {
            return !Model.delay.isExpression && Model.delay.GetValue(null) == 0;
        }
        
#if UNITY_EDITOR
        protected override void DrawCustomGUI(Rect p_rect)
        {
            GUI.color = Color.white;
            var style = new GUIStyle();
            style.alignment = TextAnchor.MiddleLeft;

            Rect labelRect = new Rect(p_rect.x + 24, p_rect.y + DashEditorCore.EditorConfig.theme.TitleTabHeight, p_rect.width-48, 20);

            
            style.normal.textColor = Color.white;

            GUI.Label(labelRect, Model.target, style);
            // Model.targetPath = GUI.TextField(new Rect(offsetRect.x + 24, offsetRect.y + 80, Size.x - 48, 20),
            //     Model.targetPath);
        }
#endif
    }
}