using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using mRemoteUG.Connection;
using mRemoteUG.Container;
using mRemoteUG.Tree.Root;


namespace mRemoteUG.Tree
{
    public sealed class ConnectionTreeModel : INotifyCollectionChanged, INotifyPropertyChanged
    {
        public List<ContainerInfo> RootNodes { get; } = new List<ContainerInfo>();

        public void AddRootNode(ContainerInfo rootNode)
        {
            if (RootNodes.Contains(rootNode)) return;
            RootNodes.Add(rootNode);
            rootNode.CollectionChanged += RaiseCollectionChangedEvent;
            rootNode.PropertyChanged += RaisePropertyChangedEvent;
            RaiseCollectionChangedEvent(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, rootNode));
        }


        public IReadOnlyList<ConnectionInfo> GetRecursiveChildList()
        {
            var list = new List<ConnectionInfo>();
            foreach (var rootNode in RootNodes)
            {
                list.AddRange(GetRecursiveChildList(rootNode));
            }
            return list;
        }

        public IEnumerable<ConnectionInfo> GetRecursiveChildList(ContainerInfo container)
        {
            return container.GetRecursiveChildList();
        }

        public void RenameNode(ConnectionInfo connectionInfo, string newName)
        {
            if (newName == null || newName.Length <= 0)
                return;

            connectionInfo.Name = newName;
            if (Settings.Default.SetHostnameLikeDisplayName)
                connectionInfo.Hostname = newName;
        }

        public void DeleteNode(ConnectionInfo connectionInfo)
        {
            if (connectionInfo is RootNodeInfo)
                return;
            
            connectionInfo?.RemoveParent();
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;
        private void RaiseCollectionChangedEvent(object? sender, NotifyCollectionChangedEventArgs args)
        {
            CollectionChanged?.Invoke(sender, args);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChangedEvent(object? sender, PropertyChangedEventArgs args)
        {
            PropertyChanged?.Invoke(sender, args);
        }
    }
}