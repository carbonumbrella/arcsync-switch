using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;

namespace ArcSyncSwitch
{
    static class LogonTask
    {
        public const string TaskName = "ArcSyncSwitch";

        public static bool Exists() => Schtasks($"/Query /TN \"{TaskName}\"") == 0;

        public static bool Create(string exe, string args, string description)
        {
            string user = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            string x(string s) => SecurityElement.Escape(s) ?? "";
            string xml = $"""
                <?xml version="1.0" encoding="UTF-16"?>
                <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
                  <RegistrationInfo><Description>{x(description)}</Description></RegistrationInfo>
                  <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>{x(user)}</UserId></LogonTrigger></Triggers>
                  <Principals><Principal id="Author"><UserId>{x(user)}</UserId><LogonType>InteractiveToken</LogonType>
                    <RunLevel>LeastPrivilege</RunLevel></Principal></Principals>
                  <Settings>
                    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                    <ExecutionTimeLimit>PT2M</ExecutionTimeLimit>
                    <Priority>4</Priority>
                  </Settings>
                  <Actions Context="Author"><Exec><Command>{x(exe)}</Command><Arguments>{x(args)}</Arguments>
                    <WorkingDirectory>{x(Path.GetDirectoryName(exe) ?? "")}</WorkingDirectory></Exec></Actions>
                </Task>
                """;
            string file = Path.Combine(Path.GetTempPath(), $"{TaskName}-task.xml");
            try
            {
                File.WriteAllText(file, xml, Encoding.Unicode);
                return Schtasks($"/Create /TN \"{TaskName}\" /XML \"{file}\" /F") == 0;
            }
            finally
            {
                try { File.Delete(file); } catch { }
            }
        }

        public static void Delete()
        {
            if (Exists()) Schtasks($"/Delete /TN \"{TaskName}\" /F");
        }

        static int Schtasks(string args)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo("schtasks.exe", args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                })!;
                p.StandardOutput.ReadToEnd();
                p.StandardError.ReadToEnd();
                p.WaitForExit(10000);
                return p.ExitCode;
            }
            catch { return -1; }
        }
    }
}
