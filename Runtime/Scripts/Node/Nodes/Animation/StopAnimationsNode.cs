/*
 *	Created by:  Peter @sHTiF Stefcek
 */

using System;
using Dash.Attributes;
using UnityEngine;

namespace Dash
{
    [Attributes.Tooltip("Stop all or specific target animations.")]
    [Category(NodeCategoryType.ANIMATION)]
    [OutputCount(1)]
    [InputCount(1)]
    [Size(200,85)]
    [Serializable]
    public class StopAnimationsNode : RetargetNodeBase<StopAnimationsNodeModel>
    {
        protected override void ExecuteOnTarget(Transform p_target, NodeFlowData p_flowData)
        {
            // Restored: dead since the 2021 refactor removed StopActiveTweens. Now backed by the
            // execution registry — kills matching animation tweens across every live flow (with
            // exact frame accounting), including this node's own flow's other branches, which is
            // the historical behavior. This flow's own frame is not tween-backed, so the node
            // continues normally below.
            Graph.StopAnimations(Model.allAnimations ? null : p_target);

            OnExecuteEnd(p_flowData);
            OnExecuteOutput(0, p_flowData);
        }
    }
}