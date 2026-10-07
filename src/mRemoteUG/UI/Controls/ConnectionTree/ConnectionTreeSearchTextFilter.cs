using System;
using System.Collections.Generic;

using mRemoteUG.Connection;
using mRemoteUG.Tree;

namespace mRemoteUG.UI.Controls
{
    public class ConnectionTreeSearchTextFilter : IConnectionTreeNodeFilter
    {
        public string FilterText { get; set; } = "";

        /// <summary>
        /// A list of <see cref="ConnectionInfo"/> objects that should
        /// always be included in the output, regardless of matching
        /// the desired <see cref="FilterText"/>.
        /// </summary>
        public List<ConnectionInfo> SpecialInclusionList { get; } = new List<ConnectionInfo>();

        public bool Filter(object modelObject)
        {
            var objectAsConnectionInfo = modelObject as ConnectionInfo;
            if (objectAsConnectionInfo == null)
                return false;

            if (SpecialInclusionList.Contains(objectAsConnectionInfo))
                return true;

            // Three ToLowerInvariant allocations per node per keystroke, and each of those
            // three reads goes through ConnectionInfo's reflection-backed inheritance lookup.
            // OrdinalIgnoreCase allocates nothing and is also the correct comparison: the
            // culture-sensitive lower-casing this replaces folds I to a dotless i in Turkish.
            return objectAsConnectionInfo.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
                   || objectAsConnectionInfo.Hostname.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
                   || objectAsConnectionInfo.Description.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
        }
    }
}
