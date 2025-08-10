/*
 *	Created by:  Peter @sHTiF Stefcek
 */

using System;
using System.Runtime.InteropServices.ComTypes;
using Dash.Attributes;
using Dash.Enums;
using UnityEngine;

namespace Dash
{
    [Serializable]
    public class AnimateColorNodeModel : AnimationNodeModelBase
    {
        [Order(11)]
        [TitledGroup("Properties")]
        public AlphaTargetType targetType = AlphaTargetType.CANVASGROUP;
        
        [Order(12)]
        [TitledGroup("Properties")]
        [DependencySingle("targetType", AlphaTargetType.TEXTMESHPRO)]
        [DependencySingle("targetType", AlphaTargetType.CANVASGROUP)]
        public bool alphaOnly = false;
        
        [Order(13)]
        [TitledGroup("Properties")]
        public bool useFrom = false;
        
        [Order(14)]
        [TitledGroup("Properties")]
        [Dependency("useFrom", true)]
        [DependencySingle("targetType", AlphaTargetType.CANVASGROUP)]
        [DependencySingle("targetType", AlphaTargetType.TEXTMESHPRO)]
        [Dependency("alphaOnly", true)]
        public Parameter<float> fromAlpha = new Parameter<float>(0);
        
        [Order(15)]
        [TitledGroup("Properties")]
        [Dependency("useFrom", true)]
        [Dependency("targetType", AlphaTargetType.CANVASGROUP)]
        public bool isFromRelative = true;
        
        [Order(16)]
        [TitledGroup("Properties")]
        [DependencySingle("targetType", AlphaTargetType.CANVASGROUP)]
        [DependencySingle("targetType", AlphaTargetType.TEXTMESHPRO)]
        [Dependency("alphaOnly", true)]
        public Parameter<float> toAlpha = new Parameter<float>(1);
        
        [Order(17)]
        [TitledGroup("Properties")]
        [Dependency("targetType", AlphaTargetType.CANVASGROUP)]
        public bool isToRelative = true;

        [Order(18)]
        [DependencySingle("targetType", AlphaTargetType.IMAGE)]
        [DependencySingle("targetType", AlphaTargetType.TEXTMESHPRO)]
        [Dependency("useFrom", true)]
        [Dependency("alphaOnly", false)]
        [TitledGroup("Properties")]
        public Parameter<Color> fromColor = new Parameter<Color>(Color.white);
        
        [Order(19)]
        [DependencySingle("targetType", AlphaTargetType.IMAGE)]
        [DependencySingle("targetType", AlphaTargetType.TEXTMESHPRO)]
        [Dependency("alphaOnly", false)]
        [TitledGroup("Properties")]
        public Parameter<Color> toColor = new Parameter<Color>(Color.white);
        
        [Order(20)]
        [TitledGroup("Properties")]
        public bool storeToAttribute = false;
        
        [Order(21)]
        [TitledGroup("Properties")]
        [Dependency("storeToAttribute", true)]
        public Parameter<string> storeAttributeName = new Parameter<string>("color");
    }
}