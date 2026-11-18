using NUnit.Framework;

namespace mRemoteUG.Tests;

/// <summary>
/// Assembly-wide setup. A SetUpFixture covers its own namespace and everything below it, and
/// every fixture in the assembly is in mRemoteUG.Tests or below, so NUnit runs this once before
/// every one of them, whatever order they execute in.
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
