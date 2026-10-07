using System.Windows.Forms;
using NUnit.Framework;

/// <summary>
/// Assembly-wide setup. Being in the global namespace makes NUnit run this once before every
/// fixture in the assembly, whatever order they execute in.
/// </summary>
/// <remarks>
/// Several fixtures create real WinForms controls. Visual styles have to be enabled before the
/// process creates its first window, otherwise controls fall back to the comctl32 v5 window
/// classes, which differ from v6 in both metrics and behaviour -- so the DPI and font fixtures
/// measure the wrong control, and TaskDialog.ShowDialog throws outright.
///
/// It is assembly-wide rather than per-fixture because the first window wins: a fixture that
/// enabled visual styles itself would pass alone and fail in a full run, once some earlier
/// fixture had already created one.
/// ProgramRoot.Main does the same thing for the real app.
/// </remarks>
[SetUpFixture]
public class TestAssemblySetup
{
    [OneTimeSetUp]
    public void EnableVisualStyles()
    {
        Application.EnableVisualStyles();
    }
}
