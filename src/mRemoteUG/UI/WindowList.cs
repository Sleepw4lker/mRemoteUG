using System.Collections;
using System.Globalization;

using mRemoteUG.UI.Window;

namespace mRemoteUG.UI;

public class WindowList : CollectionBase, IReadOnlyList<BaseWindow>
{
    BaseWindow IReadOnlyList<BaseWindow>.this[int index] => this[(object)index];

    IEnumerator<BaseWindow> IEnumerable<BaseWindow>.GetEnumerator()
    {
        foreach (object item in List)
        {
            yield return (BaseWindow)item;
        }
    }

    #region Public Properties
    public BaseWindow this[object index]
    {
        get
        {
            CleanUp();
            if (index is BaseWindow)
            {
                return IndexByObject(index);
            }

            if (index is int)
            {
                return IndexByNumber(Convert.ToInt32(index, CultureInfo.InvariantCulture));
            }

            throw new ArgumentException("The index must be a window or a position.", nameof(index));
        }
    }

    public new int Count
    {
        get
        {
            CleanUp();
            return List.Count;
        }
    }
    #endregion

    #region Public Methods
    public void Add(BaseWindow uiWindow)
    {
        List.Add(uiWindow);
        //AddHandler uiWindow.FormClosing, AddressOf uiFormClosing
    }

    public void AddRange(BaseWindow[] uiWindow)
    {
        foreach (BaseWindow uW in uiWindow)
        {
            List.Add(uW);
        }
    }

    public void Remove(BaseWindow uiWindow)
    {
        List.Remove(uiWindow);
    }

    public BaseWindow? FromString(string uiWindow)
    {
        CleanUp();
        for (int i = 0; i < List.Count; i++)
        {
            if (this[i].Text == uiWindow.Replace("&", "&&"))
            {
                return this[i];
            }
        }

        return null;
    }
    #endregion


    private void CleanUp()
    {
        for (int i = 0; i <= List.Count - 1; i++)
        {
            if (i > List.Count - 1)
            {
                CleanUp();
                return;
            }
            var baseWindow = List[i] as BaseWindow;
            if (baseWindow != null && !baseWindow.IsDisposed)
            {
                continue;
            }

            List.RemoveAt(i);
            CleanUp();
            return;
        }
    }

    private BaseWindow IndexByObject(object index)
    {
        try
        {
            int objectIndex = List.IndexOf(index);
            return IndexByNumber(objectIndex);
        }
        catch (ArgumentOutOfRangeException e)
        {
            throw new ArgumentOutOfRangeException(e.ParamName, "Object was not present in the collection.");
        }
    }

    private BaseWindow IndexByNumber(int index)
    {
        try
        {
            // Every caller runs CleanUp first, which removes each entry that is not a live
            // BaseWindow, and the indexer throws for a position out of range.
            return (BaseWindow)List[index]!;
        }
        catch (ArgumentOutOfRangeException e)
        {
            throw new ArgumentOutOfRangeException(e.ParamName, e.ActualValue, "Index was out of bounds");
        }
    }

    /*
		private void uiFormClosing(object sender, FormClosingEventArgs e)
		{
			List.Remove(sender);
		}
    */
}
