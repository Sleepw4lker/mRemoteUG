using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using mRemoteUG.Connection;

namespace mRemoteUG.Tree
{
    /// <summary>
    /// Drag payload for connection tree nodes. Replaces ObjectListView's OLVDataObject,
    /// which the connection panel tab strip used to recognise by cast.
    /// </summary>
    /// <remarks>
    /// The connections themselves are deliberately never handed to <see cref="DataObject.SetData(string, object)"/>.
    /// A DataObject serialises non-trivial managed types when the payload crosses the OLE
    /// boundary, and that route used BinaryFormatter, which was removed in .NET 9. What goes on
    /// the data object is therefore a plain string token; the connections stay in this process,
    /// held by the instance that created them.
    ///
    /// Every drag here starts and ends inside mRemoteUG, so the token is always resolvable. A
    /// payload dropped on another application resolves to nothing, which is the correct outcome.
    /// </remarks>
    public class ConnectionInfoDataObject : DataObject
    {
        public const string Format = "mRemoteUG.ConnectionInfoList";

        /// <summary>
        /// The payload currently being dragged. Only one drag can be in flight at a time, so a
        /// single slot is enough, and it cannot accumulate entries the way a dictionary could.
        /// </summary>
        private static ConnectionInfoDataObject _inFlight;

        private readonly string _token;

        public IList<ConnectionInfo> ModelObjects { get; }

        public ConnectionInfoDataObject(params ConnectionInfo[] models)
        {
            ModelObjects = models?.Where(m => m != null).ToList() ?? new List<ConnectionInfo>();
            _token = Guid.NewGuid().ToString("N");

            _inFlight = this;
            SetData(Format, _token);
        }

        /// <summary>
        /// Drops this payload's claim on the in-flight slot. Call once the drag has finished;
        /// <see cref="Control.DoDragDrop"/> is synchronous, so a finally block is the right place.
        /// </summary>
        public void Release()
        {
            if (ReferenceEquals(_inFlight, this))
                _inFlight = null;
        }

        /// <summary>
        /// Pulls the dragged connections out of a drag payload. Recognises both the instance
        /// itself and the token, since the receiving side is not guaranteed to be handed back the
        /// original object.
        /// </summary>
        public static bool TryGetConnections(IDataObject data, out IList<ConnectionInfo> models)
        {
            models = null;

            if (data is ConnectionInfoDataObject own)
            {
                models = own.ModelObjects;
                return models.Count > 0;
            }

            if (TryGetToken(data, out var token))
            {
                var inFlight = _inFlight;
                if (inFlight != null && inFlight._token == token)
                    models = inFlight.ModelObjects;
            }

            return models != null && models.Count > 0;
        }

        /// <summary>
        /// Reads the token, preferring <see cref="ITypedDataObject"/> where the payload offers it.
        /// DataObject implements that interface from .NET 10 onward, and the untyped GetData
        /// overloads are obsolete there; the fallback covers any IDataObject that does not.
        /// </summary>
        private static bool TryGetToken(IDataObject data, out string token)
        {
            token = null;
            if (data == null || !data.GetDataPresent(Format))
                return false;

            if (data is ITypedDataObject typed)
                return typed.TryGetData(Format, out token) && token != null;

            token = data.GetData(Format) as string;
            return token != null;
        }
    }
}
