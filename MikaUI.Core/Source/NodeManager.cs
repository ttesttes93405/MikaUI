using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;



namespace MikaUI
{

    internal enum NodeStatus
    {
        None,
        Created,
        BeforeRecovered,
        Recovered,
    }

    sealed record Node
    {
        public Guid TokenID { get; init; }
        public string Name { get; init; }
        public Guid? ParentTokenID { get; set; }
        public List<Guid> Children { get; } = new();
        public NodeStatus Status { get; private set; }
        public Action BeforeRecoverySelf { get; init; }
        public Action AfterRecoverySelf { get; init; }
        public Action UnexpectedRecoverySelf { get; init; }
        public Action OnRecoveryCompleted { get; init; }

        public Node(Guid tokenID, string name)
        {
            TokenID = tokenID;
            Name = name;
        }

        public void ChangeStatus(NodeStatus from, NodeStatus to)
        {
            if (Status != from)
            {
                throw new Exception($"Cannot change status from {from} to {to} for node {Name}, because current status is {Status}");
            }
            Status = to;
        }

        public bool ValidateStatus(NodeStatus expected)
        {
            return Status == expected;
        }

        public override string ToString()
        {
            return $"Node({Name}#{TokenID.ToString().ToUpper()[^6..]})";
        }
    }

    internal class NodeManager : IDisposable
    {
        sealed class UIIdentityComparer : IEqualityComparer<IBaseUI>
        {
            public bool Equals(IBaseUI x, IBaseUI y) => ReferenceEquals(x, y);
            public int GetHashCode(IBaseUI ui) => RuntimeHelpers.GetHashCode(ui);
        }

        readonly HashSet<Guid> rootNodeIds = new();
        readonly Dictionary<Guid, Node> treeNodes = new();
        readonly Dictionary<IBaseUI, Node> uiToNode = new(new UIIdentityComparer());
        readonly Logger logger;

        public NodeManager(Logger logger)
        {
            this.logger = logger;
        }

        public void AttachNode(IBaseUI parentUI, Node node, IBaseUI ui)
        {
            if (parentUI == null)
            {
                AttachNode((Node)null, node, ui);
                return;
            }

            if (uiToNode.TryGetValue(parentUI, out var parentNode) == false)
            {
                throw new Exception($"Parent UI {parentUI} not found in treeNodes. It must be created by this UIManager.");
            }
            AttachNode(parentNode, node, ui);
        }

        void AttachNode(Node parentNode, Node node, IBaseUI ui)
        {
            uiToNode.Add(ui, node);
            AttachNode(parentNode, node);
        }

        public void AttachNode(IBaseUI parentUI, Node node)
        {
            if (parentUI == null)
            {
                AttachNode((Node)null, node);
                return;
            }

            if (uiToNode.TryGetValue(parentUI, out var parentNode) == false)
            {
                throw new Exception($"Parent UI {parentUI} not found in treeNodes. It must be created by this UIManager.");
            }
            AttachNode(parentNode, node);
        }

        void AttachNode(Node parentNode, Node node)
        {
            if (treeNodes.ContainsKey(node.TokenID))
            {
                logger?.LogError?.Invoke($"Node id: {node.TokenID} already exists in treeNodes.");
                return;
            }

            node.ChangeStatus(from: NodeStatus.None, to: NodeStatus.Created);

            treeNodes.Add(node.TokenID, node);

            if (node.ParentTokenID != null)
            {
                logger?.LogError?.Invoke($"Node {node} already has a parent with TokenID {node.ParentTokenID}.");
                return;
            }

            if (parentNode == null)
            {
                rootNodeIds.Add(node.TokenID);
                node.ParentTokenID = null;
            }
            else
            {
                parentNode.Children.Add(node.TokenID);
                node.ParentTokenID = parentNode.TokenID;
            }

            // logger?.Log?.Invoke($"+ Attached {node} to parent {parentNode}\n{LogTree()}");
        }


        public void DetachNode(Node node, IBaseUI ui)
        {
            uiToNode.Remove(ui);
            DetachNode(node);
        }

        /// <summary>
        /// Removes a node whose Unity object disappeared before normal recovery.
        /// This deliberately bypasses the BeforeRecovered state because recovery
        /// hooks are not safe to run against an already-destroyed object.
        /// </summary>
        public void DetachUnexpectedNode(Node node, IBaseUI ui)
        {
            uiToNode.Remove(ui);

            if (node.Status == NodeStatus.Created)
            {
                node.ChangeStatus(from: NodeStatus.Created, to: NodeStatus.Recovered);
            }

            if (node.ParentTokenID.HasValue && treeNodes.TryGetValue(node.ParentTokenID.Value, out var parentNode))
            {
                parentNode.Children.Remove(node.TokenID);
                node.ParentTokenID = null;
            }
            else
            {
                rootNodeIds.Remove(node.TokenID);
            }

            treeNodes.Remove(node.TokenID);
        }

        public void DetachNode(Node node)
        {
            node.ChangeStatus(from: NodeStatus.BeforeRecovered, to: NodeStatus.Recovered);

            if (node.ParentTokenID.HasValue && treeNodes.TryGetValue(node.ParentTokenID.Value, out var parentNode))
            {
                parentNode.Children.Remove(node.TokenID);
                node.ParentTokenID = null;
            }
            else if (rootNodeIds.Contains(node.TokenID))
            {
                rootNodeIds.Remove(node.TokenID);
            }
            else
            {
                logger?.LogError?.Invoke($"Cannot find parent for {node}");
            }

            treeNodes.Remove(node.TokenID);

            // logger?.Log?.Invoke($"- Detached {node} from parent\n{LogTree()}"); 
        }


        public bool TryGetNode(Guid tokenID, out Node node)
        {
            return treeNodes.TryGetValue(tokenID, out node);
        }


        public Node[] GetRootNodes()
        {
            return rootNodeIds.Select(id => treeNodes[id]).ToArray();
        }

        public string LogTree()
        {
            StringBuilder sb = new();
            foreach (var rootId in rootNodeIds)
            {
                if (treeNodes.TryGetValue(rootId, out var rootNode))
                {
                    LogNode(rootNode, 0);
                }
            }

            void LogNode(Node node, int indent)
            {
                sb.AppendLine($"{new string('.', indent * 2)}- {node}");
                foreach (var childId in node.Children)
                {
                    if (treeNodes.TryGetValue(childId, out var childNode))
                    {
                        LogNode(childNode, indent + 1);
                    }
                }
            }
            return sb.ToString();
        }


        public void Dispose()
        {
            rootNodeIds.Clear();
            treeNodes.Clear();
            uiToNode.Clear();
        }
    }



}
