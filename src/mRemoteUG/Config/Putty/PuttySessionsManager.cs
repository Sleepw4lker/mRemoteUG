using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using mRemoteUG.Tools;
using mRemoteUG.Tree.Root;
// ReSharper disable ArrangeAccessorOwnerBody

namespace mRemoteUG.Config.Putty
{
    public class PuttySessionsManager
	{
        public static PuttySessionsManager Instance { get; } = new PuttySessionsManager();

        private readonly List<AbstractPuttySessionsProvider> _providers = new List<AbstractPuttySessionsProvider>();
        public IEnumerable<AbstractPuttySessionsProvider> Providers
        {
            get { return _providers; }
        }

	    public List<RootPuttySessionsNodeInfo> RootPuttySessionsNodes { get; } = new List<RootPuttySessionsNodeInfo>();

	    /// <summary>
	    /// Marshals watcher-driven refreshes onto the thread <see cref="StartWatcher"/> was called
	    /// from. Null until then, and again after <see cref="StopWatcher"/>.
	    /// </summary>
	    private CoalescingDispatcher? _watcherRefresh;

	    private PuttySessionsManager()
	    {
	        AddProvider(new PuttySessionsRegistryProvider());
	    }


        #region Public Methods
		public void AddSessions()
		{
			foreach (var provider in Providers)
			{
			    AddSessionsFromProvider(provider);
			}
		}

	    private void AddSessionsFromProvider(AbstractPuttySessionsProvider puttySessionProvider)
	    {
	        puttySessionProvider.ThrowIfNull(nameof(puttySessionProvider));

            var rootTreeNode = puttySessionProvider.RootInfo;
	        puttySessionProvider.GetSessions();

            if (!RootPuttySessionsNodes.Contains(rootTreeNode) && rootTreeNode.HasChildren())
                RootPuttySessionsNodes.Add(rootTreeNode);
            rootTreeNode.SortRecursive();
        }
		
		/// <remarks>
		/// Called on the UI thread during startup, which is where the dispatcher gets the context
		/// it later posts refreshes to.
		/// </remarks>
		public void StartWatcher()
		{
			_watcherRefresh ??= new CoalescingDispatcher(AddSessions);

			foreach (var provider in Providers)
			{
				provider.StartWatcher();
				provider.PuttySessionChanged += PuttySessionChanged;
			}
		}

		public void StopWatcher()
		{
			foreach (var provider in Providers)
			{
				provider.StopWatcher();
				provider.PuttySessionChanged -= PuttySessionChanged;
			}

			_watcherRefresh?.Stop();
			_watcherRefresh = null;
		}

        public void AddProvider(AbstractPuttySessionsProvider newProvider)
        {
            if (_providers.Contains(newProvider)) return;
            _providers.Add(newProvider);
            newProvider.PuttySessionsCollectionChanged += RaisePuttySessionCollectionChangedEvent;
        }

        /// <summary>
        /// Arrives on a thread pool thread from the registry watcher. The refresh itself must not
        /// run there: it mutates and enumerates the PuTTY Profile tree, which the UI thread reads
        /// whenever that subtree is expanded, filtered or rebuilt, and the connection property grid
        /// reads through <see cref="SessionList"/>. Doing the work off-thread crashed the process
        /// with "Collection was modified; enumeration operation may not execute" when a profile was
        /// saved in PuTTY.
        /// </summary>
        public void PuttySessionChanged(object sender, PuttySessionChangedEventArgs e)
		{
			var refresh = _watcherRefresh;
			if (refresh == null) return;   // watcher stopped; nothing left to refresh

			refresh.Request();
		}
        #endregion

        #region Private Methods
		private string[] GetSessionNames(bool raw = false)
		{
			var sessionNames = new List<string>();
			foreach (var provider in Providers)
			{
				sessionNames.AddRange(provider.GetSessionNames(raw));
			}
			return sessionNames.ToArray();
		}
			
        #endregion
			
        #region Public Classes
        public class SessionList : StringConverter
        {
            public static string[] Names
            {
                get { return Instance.GetSessionNames(); }
            }

            public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
	        {
		        return new StandardValuesCollection(Names);
	        }
				
	        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
	        {
		        return true;
	        }
				
	        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
	        {
		        return true;
	        }
        }
        #endregion

        public event NotifyCollectionChangedEventHandler PuttySessionsCollectionChanged;
        protected void RaisePuttySessionCollectionChangedEvent(object? sender, NotifyCollectionChangedEventArgs args)
        {
            PuttySessionsCollectionChanged?.Invoke(sender, args);
        }

    }
}