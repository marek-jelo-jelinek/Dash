/*
 *	Created by:  Peter @sHTiF Stefcek
 */

using System;
using System.Collections.Generic;
using System.Linq;
using OdinSerializer;
using OdinSerializer.Utilities;
using UnityEngine;
using LinqExtensions = OdinSerializer.Utilities.LinqExtensions;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using Dash.Editor;
using UnityEditor;
#endif

namespace Dash
{
    [Serializable]
    public class DashGraph : ScriptableObject, ISerializationCallbackReceiver, IVariableOwner
    {
        public int version { get; private set; } = 0;

        public IVariableBindable Bindable => null;

        [field: NonSerialized]
        public event Action<OutputNode, NodeFlowData> OnOutput;
        
        [SerializeField]
        private DashVariables _variables;

        public DashVariables variables
        {
            get
            {
                if (_variables == null)
                    _variables = new DashVariables();

                return _variables;
            }
        }

        private ExtractedClipCache _extractedClipCache;

        public ExtractedClipCache ExtractedClipCache
        {
            get
            {
                if (_extractedClipCache == null)
                {
                    _extractedClipCache = new ExtractedClipCache();
                }

                return _extractedClipCache;
            } 
        }
        
        [SerializeField]
        private List<NodeBase> _nodes = new List<NodeBase>();

        public List<NodeBase> Nodes => _nodes;

        [SerializeField]
        private List<NodeConnection> _connections = new List<NodeConnection>();
        
        public List<NodeConnection> Connections => _connections;

        [NonSerialized]
        private Dictionary<string, List<EventHandler>> _nodeListeners = new Dictionary<string, List<EventHandler>>();

        [NonSerialized]
        private Dictionary<string, List<EventHandler>>
            _callbackListeners = new Dictionary<string, List<EventHandler>>();

        [NonSerialized]
        private DashGraph _parentGraph;

        public DashGraph GetParentGraph()
        {
            return _parentGraph;
        }

        public DashGraph RootGraph
        {
            get
            {
                if (_parentGraph == null)
                    return this;

                return _parentGraph.RootGraph;
            }
        }

        public string GraphPath
        {
            get
            {
                if (_parentGraph != null)
                    return _parentGraph.GraphPath + "/"+ name;

                return name;
            }
        }

        [NonSerialized]
        protected bool _initialized = false;

        [field: NonSerialized]
        public DashController Controller { get; private set; }

        public int CurrentExecutionCount => Nodes.Sum(n => n.ExecutionCount);

        public void Initialize(DashController p_controller)
        {
            if (_initialized)
                return;

            Controller = p_controller;

            _nodes.ForEach(n => n.Initialize());
            variables.Initialize(p_controller);
            
            _initialized = true;
        }

        public GraphExecution SendEvent(string p_name, Transform p_target)
        {
            NodeFlowData flowData = new NodeFlowData();
            flowData.SetAttribute(DashReservedParameterNames.TARGET, p_target);

            return SendEvent(p_name, flowData);
        }

        /// <summary>
        /// Sends an event into this graph and returns the flow's <see cref="GraphExecution"/> so
        /// the triggered cascade can be stopped like any other flow. All listeners of one send
        /// share the one execution. When the flow data already carries an execution (an in-graph
        /// resend), that original execution is returned.
        /// </summary>
        public GraphExecution SendEvent(string p_name, NodeFlowData p_flowData)
        {
            p_flowData.SetAttribute(DashReservedParameterNames.EVENT, p_name);
            GraphExecution execution = EnsureExecution(p_flowData, ExecutionOriginType.EVENT, p_name);

            if (_nodeListeners.ContainsKey(p_name))
            {
                _nodeListeners[p_name].ToList().ForEach(e =>
                {
                    if (e.Once) _nodeListeners[p_name].Remove(e);
                    e.Invoke(p_flowData);
                });
            }

            if (_callbackListeners.ContainsKey(p_name))
            {
                _callbackListeners[p_name].ToList().ForEach(c =>
                {
                    if (c.Once) _callbackListeners[p_name].Remove(c);
                    c.Invoke(p_flowData);
                });
            }

            return execution;
        }

        public void AddListener(string p_name, NodeBase p_node, int p_priority = 0, bool p_once = false)
        {
            if (!p_name.IsNullOrWhitespace())
            {
                if (!_nodeListeners.ContainsKey(p_name))
                {
                    _nodeListeners[p_name] = new List<EventHandler>();
                }

                if (!_nodeListeners[p_name].Exists(e => e.Callback == p_node.Execute))
                {
                    _nodeListeners[p_name].Add(new EventHandler(p_node.Execute, p_priority, p_once));
                    _nodeListeners[p_name] = _nodeListeners[p_name].OrderBy(e => e.Priority).ToList();
                }
            }
            else
            {
                Debug.LogWarning("Invalid event name, cannot be null or whitespace.");
            }
        }

        public void AddListener(string p_name, Action<NodeFlowData> p_callback, int p_priority = 0,
            bool p_once = false)
        {
            if (!string.IsNullOrWhiteSpace(p_name))
            {
                if (!_callbackListeners.ContainsKey(p_name))
                {
                    _callbackListeners[p_name] = new List<EventHandler>();
                }

                if (!_callbackListeners[p_name].Exists(e => e.Callback == p_callback))
                {
                    var handler = new EventHandler(p_callback, p_priority, p_once);
                    var current = _callbackListeners[p_name];
                    for (int i = current.Count - 1; i >= 0; i--)
                    {
                        if (current[i].Priority <= p_priority)
                        {
                            current.Insert(i+1, handler);
                        }                        
                    }

                    _callbackListeners[p_name].Add(new EventHandler(p_callback, p_priority, p_once));
                    _callbackListeners[p_name] = _callbackListeners[p_name].OrderBy(e => e.Priority).ToList();
                }
            }
            else
            {
                Debug.LogWarning("Invalid event name, cannot be null or whitespace.");
            }
        }
        
        public void RemoveListener(string p_name, Action<NodeFlowData> p_callback)
        {
            if (_callbackListeners.ContainsKey(p_name))
            {
                _callbackListeners[p_name].RemoveAll(e => e.Callback == p_callback);
                
                if (_callbackListeners[p_name].Count == 0)
                    _callbackListeners.Remove(p_name);
            }
        }

        public void SetListener(string p_name, Action<NodeFlowData> p_callback, int p_priority = 0, bool p_once = false)
        {
            if (_callbackListeners.ContainsKey(p_name))
            {
                _callbackListeners[p_name].Clear();
            }
            else
            {
                _callbackListeners[p_name] = new List<EventHandler>();
            }
            
            _callbackListeners[p_name].Add(new EventHandler(p_callback, p_priority, p_once));
        }

        public NodeBase GetNodeById(string p_id)
        {
            return Nodes.Find(n => n.Id == p_id);
        }

        public T GetNodeByType<T>() where T:NodeBase
        {
            return (T)Nodes.Find(n => n is T);
        }

        public NodeBase GetNodeByType(Type p_nodeType)
        {
            return Nodes.Find(n => p_nodeType.IsAssignableFrom(n.GetType()) );
        }

        public bool HasNodeOfType<T>() where T : NodeBase
        {
            return Nodes.Exists(n => n is T);
        }
        
        public bool HasNodeOfType(Type p_nodeType)
        {
            return Nodes.Exists(n => p_nodeType.IsAssignableFrom(n.GetType()));
        }

        public List<T> GetNodesByType<T>() where T : NodeBase
        {
            return Nodes.FindAll(n => n is T).ConvertAll(n => (T)n);
        }

        public bool HasInputOfName(string p_name)
        {
            return Nodes.Exists(n => n is InputNode && ((InputNode)n).Model.inputName == p_name);
        }
        
        public bool HasOutputOfName(string p_name)
        {
            return Nodes.Exists(n => n is OutputNode && ((OutputNode)n).Model.outputName == p_name);
        }
        
        public bool HasOnCustomEventOfName(string p_name)
        {
            return Nodes.Exists(n => n is OnCustomEventNode && ((OnCustomEventNode)n).Model.eventName == p_name);
        }

        public int GetOutputIndex(OutputNode p_node)
        {
            return Nodes.FindAll(n => n is OutputNode).IndexOf(p_node);
        }

        public bool Connect(NodeBase p_inputNode, int p_inputIndex, NodeBase p_outputNode, int p_outputIndex)
        {
            if (p_inputNode == p_outputNode)
                return false;
            
            bool exists = Connections.Exists(c =>
                c.inputNode == p_inputNode && c.inputIndex == p_inputIndex && c.outputNode == p_outputNode &&
                c.outputIndex == p_outputIndex);
            
            if (exists || p_inputNode.InputCount <= p_inputIndex || p_outputNode.OutputCount <= p_outputIndex) 
                return false;
            
            NodeConnection connection = new NodeConnection(p_inputIndex, p_inputNode, p_outputIndex, p_outputNode);
            
            _connections.Add(connection);
            return true;
        }

        public void Disconnect(NodeConnection p_connection)
        {
            _connections.Remove(p_connection);
            p_connection.inputNode.OnConnectionRemoved?.Invoke(p_connection);
        }

        public void DisconnectNode(NodeBase p_node)
        {
            List<NodeConnection> connections =
                Connections.FindAll(c => c.inputNode == p_node || c.outputNode == p_node);

            foreach (var connection in connections)
            {
                if (Connections.Contains(connection))
                {
                    Disconnect(connection);
                }
            }
        }

        public void ExecuteNodeOutputs(NodeBase p_node, int p_index, NodeFlowData p_flowData)
        {
            foreach (var connection in _connections)
            {
                if (connection.active && connection.outputNode == p_node && connection.outputIndex == p_index)
                {
                    connection.Execute(p_flowData);
                }
            }
        }

        public bool HasOutputConnected(NodeBase p_node, int p_index)
        {
            return _connections.Exists(c => c.outputNode == p_node && c.outputIndex == p_index);
        }
        
        public bool HasInputConnected(NodeBase p_node, int p_index)
        {
            return _connections.Exists(c => c.inputNode == p_node && c.inputIndex == p_index);
        }
        
        public bool HasAnyInputConnected(NodeBase p_node)
        {
            return _connections.Exists(c => c.inputNode == p_node);
        }
        
        public List<NodeConnection> GetInputConnections(NodeBase p_node)
        {
            return _connections.FindAll(c => c.inputNode == p_node);
        }
        
        public List<NodeConnection> GetOutputConnections(NodeBase p_node)
        {
            return _connections.FindAll(c => c.outputNode == p_node);
        }

        protected void RemoveNodeConnections(NodeBase p_node)
        {
            Connections.RemoveAll(c => c.outputNode == p_node || c.inputNode == p_node);
        }

        public DashGraph Clone()
        {
            List<Object> references = new List<Object>();
            byte[] bytes = this.SerializeToBytes(DataFormat.Binary, ref references);
            
            DashGraph graph = CreateInstance<DashGraph>();

            for (int i = 0; i < references.Count; i++)
            {
                if (references[i] == this)
                    references[i] = graph;
            }
            
            graph.DeserializeFromBytes(bytes, DataFormat.Binary, ref references);
            graph.name = name;
            return graph;
        }

        // Live executions minted by this graph instance. Lifetime rule: an execution with open
        // frames is in flight; one at zero frames has completed (a queued/waiting flow always
        // holds a frame on its waiting node, and hops are synchronous, so zero is never observed
        // mid-hop from the main thread). Completed and stopped entries are pruned on each mint,
        // bounding growth without needing a completion signal.
        [NonSerialized]
        private List<GraphExecution> _executions;

        /// <summary>Executions of this graph instance that are currently in flight.</summary>
        public int LiveExecutionCount
        {
            get
            {
                if (_executions == null)
                    return 0;

                int count = 0;
                for (int i = 0; i < _executions.Count; i++)
                {
                    if (!_executions[i].IsStopped && _executions[i].TotalFrames > 0)
                        count++;
                }

                return count;
            }
        }

        /// <summary>
        /// Mints a new <see cref="GraphExecution"/> owned by this graph.
        /// </summary>
        public GraphExecution CreateExecution()
        {
            PruneExecutions();

            GraphExecution execution = new GraphExecution(DashCore.Instance.NextExecutionId(), this);
            _executions.Add(execution);
            return execution;
        }

        // Drops completed (zero frames anywhere) and stopped entries. Called on every mint AND
        // every register-on-entry, so a graph that only ever receives foreign executions (a
        // cross-controller event receiver, a subgraph) cannot grow its registry unboundedly.
        private void PruneExecutions()
        {
            if (_executions == null)
                _executions = new List<GraphExecution>();
            else
                _executions.RemoveAll(e => e.IsStopped || e.TotalFrames == 0);
        }

        // Register-on-entry: an execution minted elsewhere (cross-controller event cascade, a flow
        // entering a subgraph) becomes addressable from THIS graph's registry too, so queries and
        // graph-scoped stops on the receiving side can find it.
        internal void RegisterExecution(GraphExecution p_execution)
        {
            if (p_execution == null)
                return;

            PruneExecutions();

            if (!_executions.Contains(p_execution))
                _executions.Add(p_execution);
        }

        /// <summary>
        /// Ensures a flow has an execution assigned, minting one if it entered without an identity.
        /// The origin is stamped ONLY on mint — a flow arriving with an execution keeps its
        /// original origin. Called at flow origins; a null flow data is a no-op (NodeBase.Execute
        /// mints instead).
        /// </summary>
        internal GraphExecution EnsureExecution(NodeFlowData p_flowData,
            ExecutionOriginType p_originType = ExecutionOriginType.NONE, string p_originName = null)
        {
            if (p_flowData == null)
                return null;

            if (p_flowData.execution == null)
            {
                p_flowData.execution = CreateExecution();
                p_flowData.execution.SetOrigin(p_originType, p_originName, p_flowData);
            }
            else
            {
                RegisterExecution(p_flowData.execution);
            }

            return p_flowData.execution;
        }

        public bool ExecuteGraphInput(string p_inputName, NodeFlowData p_flowData)
        {
            return ExecuteGraphInput(p_inputName, p_flowData, out _);
        }

        /// <summary>
        /// Runs a named input and hands back the <see cref="GraphExecution"/> that now owns the
        /// flow, so the caller can later stop exactly that flow via DashController.Stop(execution)
        /// or execution.Stop(). p_execution is null when no such input exists (or the flow was
        /// started with a null flow data, which mints its execution internally).
        /// </summary>
        public bool ExecuteGraphInput(string p_inputName, NodeFlowData p_flowData, out GraphExecution p_execution)
        {
            p_execution = null;

            InputNode inputNode = GetNodesByType<InputNode>().Find(n => n.Model.inputName == p_inputName);
            if (inputNode == null)
            {
                Debug.LogWarning("There is no input with name "+p_inputName);
                return false;
            }

            p_execution = EnsureExecution(p_flowData, ExecutionOriginType.INPUT, p_inputName);
            inputNode.Execute(p_flowData);
            return true;
        }

        public void Stop()
        {
            // Tear down every in-flight execution first so their disposables run — freeing
            // sequencer slots (the historic whole-graph-stop deadlock) and despawning what
            // interrupted flows created. Completed executions (zero frames) are dropped without
            // disposal: their products stay. Swap the list out first — a disposal can start new
            // flows (sequencer advance), which must land in a fresh registry.
            if (_executions != null)
            {
                List<GraphExecution> executions = _executions;
                _executions = null;

                foreach (GraphExecution execution in executions)
                {
                    // Own only flows currently running IN this graph. A shared cascade that
                    // already finished its part here (still running in another controller's
                    // graph) is left alone; one that IS running here is one identity, so
                    // stopping it tears it down everywhere.
                    if (!execution.IsStopped && execution.HasFramesIn(this))
                        execution.Stop();
                }
            }

            // Node-level sweep: legacy/execution-less tweens (editor preview) and count reset.
            Nodes.ForEach(n => n.Stop());
        }

        /// <summary>Stops a single flow (see <see cref="GraphExecution.Stop"/>), not the whole graph.</summary>
        public void Stop(GraphExecution p_execution)
        {
            p_execution?.Stop();
        }

        /// <summary>
        /// Kills animation tweens on p_target across every live execution of this graph (null =
        /// every target), with exact frame accounting — the per-target stop the 2021 refactor
        /// removed. Flows are not stopped: branches whose animation died just end, everything else
        /// keeps running. Returns the number of tweens killed.
        /// </summary>
        public int StopAnimations(Transform p_target)
        {
            if (_executions == null)
                return 0;

            int killed = 0;

            for (int i = 0; i < _executions.Count; i++)
            {
                if (!_executions[i].IsStopped)
                    killed += _executions[i].KillTweensByTarget(p_target);
            }

            return killed;
        }

        /// <summary>Live (in-flight, not stopped) execution with this id, or null.</summary>
        public GraphExecution GetExecution(ExecutionId p_id)
        {
            if (_executions == null)
                return null;

            for (int i = 0; i < _executions.Count; i++)
            {
                if (_executions[i].id == p_id && !_executions[i].IsStopped)
                    return _executions[i];
            }

            return null;
        }

        /// <summary>Stops the live execution with this id. Returns false when none exists.</summary>
        public bool Stop(ExecutionId p_id)
        {
            GraphExecution execution = GetExecution(p_id);
            if (execution == null)
                return false;

            execution.Stop();
            return true;
        }

        /// <summary>Stops every live flow started from the named graph input. Returns the count stopped.</summary>
        public int StopExecutionsByInput(string p_inputName)
        {
            return StopExecutionsMatching(e =>
                e.OriginType == ExecutionOriginType.INPUT && e.OriginName == p_inputName);
        }

        /// <summary>Stops every live flow started by the named event. Returns the count stopped.</summary>
        public int StopExecutionsByEvent(string p_eventName)
        {
            return StopExecutionsMatching(e =>
                e.OriginType == ExecutionOriginType.EVENT && e.OriginName == p_eventName);
        }

        /// <summary>
        /// Stops every live flow whose INITIAL target was p_target (later retargeting does not
        /// change a flow's origin target). This is per-target FLOW stop — full teardown of the
        /// runs started on that target — as opposed to StopAnimations, which only kills tweens.
        /// </summary>
        public int StopExecutionsByTarget(Transform p_target)
        {
            return StopExecutionsMatching(e => e.OriginTarget == p_target);
        }

        // Snapshot-then-stop: a disposal run by Stop can synchronously start new flows, which
        // mint executions and mutate _executions — never iterate the live list while stopping.
        // Matches only flows currently running IN this graph (see Stop()); stopping one tears it
        // down everywhere, since an execution is one identity.
        private int StopExecutionsMatching(Predicate<GraphExecution> p_match)
        {
            if (_executions == null)
                return 0;

            List<GraphExecution> matched = new List<GraphExecution>();

            for (int i = 0; i < _executions.Count; i++)
            {
                GraphExecution execution = _executions[i];
                if (!execution.IsStopped && execution.HasFramesIn(this) && p_match(execution))
                    matched.Add(execution);
            }

            for (int i = 0; i < matched.Count; i++)
                matched[i].Stop();

            return matched.Count;
        }
        
        private HashSet<NodeBase> _downstreamNodes = new HashSet<NodeBase>();
        
        public void StopDownstream(NodeBase p_node)
        {
            _downstreamNodes.Clear();
            StopDownstreamInternal(p_node);
        }

        private void StopDownstreamInternal(NodeBase p_node)
        {
            if (_downstreamNodes.Contains(p_node))
                return;

            _downstreamNodes.Add(p_node);

            p_node.Stop();
            var connections = Connections.FindAll(c => c.outputNode == p_node);
            connections.Sort((c1, c2) =>
            {
                return c1.outputIndex.CompareTo(c2.outputIndex);
            });

            foreach (var connection in connections)
            {
                StopDownstreamInternal(connection.inputNode);
            }
        }

        public virtual void MarkDirty()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif

            if (_parentGraph != null)
            {
                _parentGraph.MarkDirty();
            }
        }

#region SERIALIZATION

        [SerializeField, HideInInspector]
        private SerializationData _serializationData;
        
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (this == null)
                return;
            
            using (var cachedContext = OdinSerializer.Utilities.Cache<DeserializationContext>.Claim())
            {
                cachedContext.Value.Config.SerializationPolicy = SerializationPolicies.Everything;
                UnitySerializationUtility.DeserializeUnityObject(this, ref _serializationData, cachedContext.Value);
            }
        }
        
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            if (this == null)
                return;
            
#if UNITY_EDITOR
            SetVersion(DashCore.GetVersionNumber());
            
            if (!Application.isPlaying)
            {
                GetNodesByType<SubGraphNode>().ForEach(n => n.ReserializeBound());

                using (var cachedContext = OdinSerializer.Utilities.Cache<SerializationContext>.Claim())
                {
                    cachedContext.Value.Config.SerializationPolicy = SerializationPolicies.Everything;
                    UnitySerializationUtility.SerializeUnityObject(this, ref _serializationData,
                        serializeUnityFields: true, context: cachedContext.Value);
                }
            }
#endif
        }
        
        public byte[] SerializeToBytes(DataFormat p_format, ref List<Object> p_references)
        {
            //Debug.Log("SerializeToBytes "+this);
            byte[] bytes = null;

            using (var cachedContext = OdinSerializer.Utilities.Cache<SerializationContext>.Claim())
            {
                cachedContext.Value.Config.SerializationPolicy = SerializationPolicies.Everything;
                UnitySerializationUtility.SerializeUnityObject(this, ref bytes, ref p_references, p_format, true,
                    cachedContext.Value);
            }

            return bytes;
        }

        public void DeserializeFromBytes(byte[] p_bytes, DataFormat p_format, ref List<Object> p_references)
        {
            //Debug.Log("DeserializeToBytes "+this);
            using (var cachedContext = OdinSerializer.Utilities.Cache<DeserializationContext>.Claim())
            {
                cachedContext.Value.Config.SerializationPolicy = SerializationPolicies.Everything;
                UnitySerializationUtility.DeserializeUnityObject(this, ref p_bytes, ref p_references, p_format,
                    cachedContext.Value);
            }
        }
#endregion

#region INTERNAL_ACCESS

        internal void SetParentGraph(DashGraph p_graph)
        {
            _parentGraph = p_graph;
        }
        
        internal void OutputExecuted(OutputNode p_node, NodeFlowData p_flowData)
        {
            OnOutput?.Invoke(p_node, p_flowData);
        }

        internal void SetVersion(int p_version)
        {
            // Disabled for now to avoid checksum changes
            //version = p_version;
        }

#endregion

#region EDITOR_CODE
#if UNITY_EDITOR

        [SerializeField]
        private List<GraphBox> _boxes = new List<GraphBox>();

        public bool previewControlsViewMinimized = true;
        public Vector2 viewOffset = Vector2.zero;
        public float zoom = 1;
        public bool graphVariablesMinimized = true;
        public bool globalVariablesMinimized = true;

        public NodeBase previewNode;

        // [NonSerialized]
        // public NodeBase connectingNode;
        // [NonSerialized]
        // public int connectingOutputIndex;

        // public void Reconnect(NodeConnection p_connection)
        // {
        //     connectingNode = p_connection.outputNode;
        //     connectingOutputIndex = p_connection.outputIndex;
        //
        //     _connections.Remove(p_connection);
        // }
        
        public void DeleteNode(NodeBase p_node)
        {
            _connections.RemoveAll(c => c.inputNode == p_node || c.outputNode == p_node);
            p_node.Remove();
            Nodes.Remove(p_node);
            
            if (previewNode == p_node) previewNode = null;
        }
        
        public void DrawGUI(Rect p_rect)
        {
            // Sometimes when looking for a serialization issue it is good to keep null references for better debug/migration
            if (DashEditorCore.EditorConfig.deleteNull)
                RemoveNullReferences();

            // Draw boxes
            LinqExtensions.ForEach(_boxes.Where(r => r != null), r => r.DrawGUI());

            _connections.RemoveAll(c => !c.IsValid());
            
            // Draw connections
            LinqExtensions.ForEach(_connections.Where(c => c != null).ToArray(), c=> c.DrawGUI());
            
            // Draw Nodes
            // Preselect non null to avoid null states from serialization issues
            LinqExtensions.ForEach(_nodes.Where(n => n != null), n => n.DrawGUI(p_rect));

            // Draw user interaction with connections
            NodeConnection.DrawConnectionToMouse(SelectionManager.connectingNode, SelectionManager.connectingIndex, SelectionManager.connectingType, SelectionManager.connectingPosition);
            
            //DashEditorCore.SetDirty();
        }

        public void DrawComments(Rect p_rect, bool p_zoomed)
        {
            LinqExtensions.ForEach(_nodes.Where(n => n != null), n => n.DrawComment(p_rect, p_zoomed));
        }

        public bool HitsNode(Vector2 p_position, out NodeBase p_node)
        {
            p_node = _nodes.AsEnumerable().Reverse().ToList().Find(n => n.rect.Contains(p_position - viewOffset));

            return p_node != null;
        }
        
        public bool HitsNode(Vector2 p_position, out NodeBase p_node, out NodeConnectorType p_connectorType, out int p_connectorIndex)
        {
            p_node = _nodes.AsEnumerable().Reverse().ToList().Find(n => n.rect.Contains(p_position - viewOffset));

            p_connectorType = NodeConnectorType.INPUT;
            p_connectorIndex = -1;
            if (p_node != null)
            {
                p_node.HitsConnector(p_position, out p_connectorType, out p_connectorIndex);
                return true;
            }

            return false;
        }

        public GraphBox HitsBoxDrag(Vector2 p_position)
        {
            return _boxes.AsEnumerable().Reverse().ToList().Find(b => b.titleRect.Contains(p_position - viewOffset));
        }
        
        public GraphBox HitsBoxResize(Vector2 p_position)
        {
            return _boxes.AsEnumerable().Reverse().ToList().Find(b => b.resizeRect.Contains(p_position - viewOffset));
        }

        public NodeConnection HitsConnection(Vector2 p_position, float p_distance)
        {
            foreach (NodeConnection connection in _connections)
            {
                if (connection.Hits(p_position, p_distance))
                {
                    return connection; 
                }
            }

            return null;
        }

        public void CreateBox(Rect p_region)
        {
            // Increase size of region to have padding
            Rect boxRect = new Rect(p_region.xMin - 20, p_region.yMin - 60, p_region.width + 40, p_region.height + 80);
            
            GraphBox box = new GraphBox("Comment", boxRect);
            _boxes.Add(box);
        }
        
        public void DeleteBox(GraphBox p_box)
        {
            _boxes.Remove(p_box);
        }

        public void RemoveNullReferences()
        {
            Nodes.RemoveAll(n => n == null);
            Connections.RemoveAll(c => c == null);
            Connections.RemoveAll(c => c.inputNode == null || c.outputNode == null);
        }

        public List<string> GetExposedGUIDs()
        {
            List<string> exposedGUIDs = new List<string>();
            Nodes.ForEach(n => exposedGUIDs.AddRange(n.GetModelExposedGUIDs()));
            // Variable ExposedReferences support removed for redundancy
            //exposedGUIDs.AddRange(variables.GetExposedGUIDs());

            return exposedGUIDs;
        }
        
        public List<string> GetExposedNodeIDs(List<PropertyName> p_properties)
        {
            List<string> exposedNodeIDs = new List<string>();
            Nodes.ForEach(n => exposedNodeIDs.AddRange(n.GetModelExposedNodeIDs(p_properties)));

            return exposedNodeIDs;
        }

        public void ResetPosition()
        {
            viewOffset = new Vector2();
        }

        public (string, Color)[] CheckValidity(bool p_verbose = false)
        {
            List<(string, Color)> messages = new List<(string, Color)>();

            List<string> nodeTypes = new List<string>();

            string path = AssetDatabase.GetAssetPath(this);

            messages.Add(("Scanning graph " + name + " at "+path, Color.white));

            List<NodeBase> nodes;
            if (p_verbose)
            {
                messages.Add(("Scanning " + Nodes.Count + " nodes.", Color.white));

                Nodes.ForEach(n =>
                {
                    var type = NodeBase.GetNodeNameFromType(n.GetType());
                    if (!nodeTypes.Contains(type))
                    {
                        nodeTypes.Add(type);
                        messages.Add((
                            type + " found " + Nodes.FindAll(n2 => n2.GetType() == n.GetType()).Count + " times.",
                            Color.white));
                    }
                });

                nodes =
                    Nodes.FindAll(n => !Connections.Exists(c => n == c.inputNode || n == c.outputNode));
                nodes?.ForEach(n => { messages.Add(("There are no connections on node: " + n.Id, Color.yellow)); });
            }

            nodes = Nodes.FindAll(n1 => Nodes.Exists(n2 => n1 != n2 && n1.Id == n2.Id));
            nodes?.ForEach(n =>
            {
                messages.Add(("Duplicate node id found on node: " + n.Id, Color.red));
            });

            nodes = Nodes.FindAll(n => n != null && n.GetType().GetAttribute<ObsoleteAttribute>() != null);
            nodes?.ForEach(n =>
            {
                messages.Add((
                    "Obsolete node type found: " + n.Id +
                    " [Should remove before updating to new version]", Color.yellow));
            });
            
            nodes = Nodes.FindAll(n => n == null);
            nodes?.ForEach(n =>
            {
                messages.Add(("Invalid null node found in graph " + name + " [Possible serialization malfunction]",
                    Color.red));
            });
            
            return messages.ToArray();
        }
#endif
#endregion
    }
}
