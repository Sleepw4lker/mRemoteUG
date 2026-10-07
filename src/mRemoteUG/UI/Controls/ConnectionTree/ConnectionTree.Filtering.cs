using System.Collections.Generic;
using System.Linq;
using mRemoteUG.Connection;
using mRemoteUG.Tree;
using System.ComponentModel;

namespace mRemoteUG.UI.Controls
{
    /// <summary>
    /// Search filtering. ObjectListView filtered rows itself; a TreeView has no such
    /// notion, so a filtered tree is simply rebuilt from the set of nodes the filter
    /// leaves visible.
    /// </summary>
    public partial class ConnectionTree
    {
        private bool _useFiltering;
        private IConnectionTreeNodeFilter _modelFilter;

        /// <summary>
        /// Master switch for filtering, driven by the UseFilterSearch setting.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool UseFiltering
        {
            get { return _useFiltering; }
            set
            {
                if (_useFiltering == value)
                    return;
                _useFiltering = value;
                UpdateFiltering();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IConnectionTreeNodeFilter ModelFilter
        {
            get { return _modelFilter; }
            set
            {
                if (ReferenceEquals(_modelFilter, value))
                    return;
                _modelFilter = value;
                UpdateFiltering();
            }
        }

        public bool IsFiltering => UseFiltering && ModelFilter != null;

        /// <summary>
        /// Every model currently represented in the tree. While filtering this is the
        /// matches plus the ancestors retained to reach them.
        /// </summary>
        public IEnumerable<ConnectionInfo> FilteredObjects => _nodeMap.Keys.ToArray();

        /// <summary>
        /// Filters tree items based on the given <see cref="filterText"/>
        /// </summary>
        /// <param name="filterText">The text to filter by</param>
        public void ApplyFilter(string filterText)
        {
            _connectionTreeSearchTextFilter.FilterText = filterText;
            _useFiltering = true;
            _modelFilter = _connectionTreeSearchTextFilter;
            UpdateFiltering();
        }

        /// <summary>
        /// Removes all item filtering from the connection tree
        /// </summary>
        public void RemoveFilter()
        {
            _useFiltering = false;
            _modelFilter = null;
            UpdateFiltering();
        }

        public void UpdateFiltering()
        {
            RebuildAll(true);
        }

        /// <summary>
        /// The set of models a rebuild should include, or null when unfiltered.
        /// </summary>
        private ICollection<ConnectionInfo> CurrentVisibleSet()
        {
            if (!IsFiltering || ConnectionTreeModel == null)
                return null;

            return ConnectionTreeFilterEvaluator.VisibleNodes(ConnectionTreeModel.RootNodes, ModelFilter);
        }
    }
}
