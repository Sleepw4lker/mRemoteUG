using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Microsoft.Win32;

using mRemoteUG.App.Info;
using mRemoteUG.Messages;
using mRemoteUG.UI.TaskDialog;

namespace mRemoteUG.App;

public static class CompatibilityChecker
{
    public static void CheckCompatibility(MessageCollector messageCollector)
    {
        CheckFipsPolicy(messageCollector);
    }

    [SuppressMessage("Performance", "CA1863",
                     Justification = "The format is a localized resource read at run time; "
                                     + "a cached CompositeFormat would freeze the UI language.")]
    private static void CheckFipsPolicy(MessageCollector messageCollector)
    {
        if (Settings.Default.OverrideFIPSCheck)
        {
            messageCollector.AddMessage(
                MessageClass.InformationMsg,
                "OverrideFIPSCheck is set. Will skip check...",
                true);
            return;
        }

        messageCollector.AddMessage(MessageClass.InformationMsg, "Checking FIPS Policy...", true);
        if (!FipsPolicyEnabled())
        {
            return;
        }

        string errorText = string.Format(
            CultureInfo.CurrentCulture,
            Language.strErrorFipsPolicyIncompatible,
            GeneralAppInfo.ProductName);
        messageCollector.AddMessage(MessageClass.ErrorMsg, errorText, true);

        DialogResult shouldIStayOrShouldIGo = CTaskDialog.MessageBox(
            GeneralAppInfo.ProductName,
            Language.strCompatibilityProblemDetected,
            errorText,
            "",
            "",
            Language.strCheckboxDoNotShowThisMessageAgain,
            ETaskDialogButtons.OkCancel,
            ESysIcons.Warning,
            ESysIcons.Warning);
        if (CTaskDialog.VerificationChecked && shouldIStayOrShouldIGo == DialogResult.OK)
        {
            messageCollector.AddMessage(MessageClass.ErrorMsg, "User requests that FIPS check be overridden", true);
            Settings.Default.OverrideFIPSCheck = true;
            Settings.Default.Save();
            return;
        }

        if (shouldIStayOrShouldIGo == DialogResult.Cancel)
        {
            Environment.Exit(1);
        }
    }

    private static bool FipsPolicyEnabled()
    {
        RegistryKey? regKey =
            Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Control\\Lsa\\FIPSAlgorithmPolicy");
        object? fipsPolicy = regKey?.GetValue("Enabled");
        if (fipsPolicy == null)
        {
            return false;
        }

        fipsPolicy = Convert.ToInt32(fipsPolicy, CultureInfo.InvariantCulture);
        return (int)fipsPolicy != 0;
    }
}
