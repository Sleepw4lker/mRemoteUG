using Microsoft.Win32;
using mRemoteUG.App.Info;
using mRemoteUG.UI.TaskDialog;
using System;
using System.Windows.Forms;
using mRemoteUG.Messages;

namespace mRemoteUG.App
{
    public static class CompatibilityChecker
    {
        public static void CheckCompatibility(MessageCollector messageCollector)
        {
            CheckFipsPolicy(messageCollector);
        }

        private static void CheckFipsPolicy(MessageCollector messageCollector)
        {
            if (Settings.Default.OverrideFIPSCheck)
            {
                messageCollector.AddMessage(MessageClass.InformationMsg, "OverrideFIPSCheck is set. Will skip check...", true);
                return;
            }

            messageCollector.AddMessage(MessageClass.InformationMsg, "Checking FIPS Policy...", true);
            if (!FipsPolicyEnabled()) return;

            var errorText = string.Format(Language.strErrorFipsPolicyIncompatible, GeneralAppInfo.ProductName);
            messageCollector.AddMessage(MessageClass.ErrorMsg, errorText, true);

            var ShouldIStayOrShouldIGo = CTaskDialog.MessageBox(Application.ProductName, Language.strCompatibilityProblemDetected, errorText, "", "", Language.strCheckboxDoNotShowThisMessageAgain, ETaskDialogButtons.OkCancel, ESysIcons.Warning, ESysIcons.Warning);
            if (CTaskDialog.VerificationChecked && ShouldIStayOrShouldIGo == DialogResult.OK)
            {
                messageCollector.AddMessage(MessageClass.ErrorMsg, "User requests that FIPS check be overridden", true);
                Settings.Default.OverrideFIPSCheck = true;
                Settings.Default.Save();
                return;
            }

            if (ShouldIStayOrShouldIGo == DialogResult.Cancel)
                Environment.Exit(1);
        }

        private static bool FipsPolicyEnabled()
        {
            var regKey = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Control\\Lsa\\FIPSAlgorithmPolicy");
            var fipsPolicy = regKey?.GetValue("Enabled");
            if (fipsPolicy == null) return false;
            fipsPolicy = Convert.ToInt32(fipsPolicy);
            return (int)fipsPolicy != 0;
        }
    }
}
