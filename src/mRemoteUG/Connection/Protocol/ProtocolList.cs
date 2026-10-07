using System.Collections;
using System.Collections.Specialized;
// ReSharper disable ArrangeAccessorOwnerBody

namespace mRemoteUG.Connection.Protocol
{
	public class ProtocolList : CollectionBase, INotifyCollectionChanged
	{
        public ProtocolBase this[int index]
		{
			get { return (ProtocolBase)List[index]; }
		}
				
        public new int Count
        {
            get { return List.Count; }
        }


	    public void Add(ProtocolBase cProt)
		{
			List.Add(cProt);
            RaiseCollectionChangedEvent(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, cProt));
        }
				
				
		public void Remove(ProtocolBase cProt)
		{
			// CollectionBase.IList.Remove throws ArgumentException when the item is not in the
			// list rather than reporting it, so removing something that was never added has to
			// be guarded rather than caught. It is a no-op, and must not raise CollectionChanged.
			if (!List.Contains(cProt)) return;
			List.Remove(cProt);
            RaiseCollectionChangedEvent(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, cProt));
		}
				
		public new void Clear()
		{
            if (Count == 0) return;
			List.Clear();
            RaiseCollectionChangedEvent(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}

	    public event NotifyCollectionChangedEventHandler CollectionChanged;
	    private void RaiseCollectionChangedEvent(object? sender, NotifyCollectionChangedEventArgs args)
	    {
	        CollectionChanged?.Invoke(sender, args);
	    }
    }
}