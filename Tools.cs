using System;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using System.Security.Principal;
using System.Threading;
using System.Diagnostics;
using Microsoft.Win32;
using System.IO;
using System.ComponentModel;

namespace webs
{
    public static class Tools
    {
        // force comma as decimal separator
        public static void ForceDotCultureSeparator()
        {
            string CultureName = Thread.CurrentThread.CurrentCulture.Name;
            CultureInfo ci = new CultureInfo(CultureName);
            if (ci.NumberFormat.NumberDecimalSeparator != ".")
            {
                ci.NumberFormat.NumberDecimalSeparator = ".";
                Thread.CurrentThread.CurrentCulture = ci;
            }
        }

        static bool IsAlreadyAddedToPath()
        {
            string executablePath = GetExePath();
            string path = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);
            return path.Contains(executablePath);
        }

        // WARNING: if user has the exe in common already existing path, it will be removed!
        internal static void ModifyUserEnvPATH(bool add)
        {
            string executablePath = GetExePath();
            string path = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);

            if (path.Contains(executablePath) == false)
            {
                if (add == true)
                {
                    path = $"{path};{executablePath}";
                    Environment.SetEnvironmentVariable("PATH", path, EnvironmentVariableTarget.User);
                    Log("Directory added to user PATH successfully.", ConsoleColor.Gray);
                    Log("NOTE: You need to restart PC or Logout/Login, for changes to take effect in certain applications, like Unity Editor.", ConsoleColor.Yellow);
                }
                else // remove
                {
                    Log("Directory is not in PATH.", ConsoleColor.Yellow);
                }
            }
            else // already added
            {
                if (add == true)
                {
                    Log("Directory is already in PATH.", ConsoleColor.Yellow);
                }
                else
                {
                    path = path.Replace(executablePath, "");
                    Environment.SetEnvironmentVariable("PATH", path, EnvironmentVariableTarget.User);
                    Log("Directory removed from user PATH successfully.", ConsoleColor.Gray);
                }
            }
        }

        public static void Log(string msg, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(msg);
        }

        // get LAN IP address starting with 192.
        public static object GetIpAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ipAddress = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork && ip.ToString().StartsWith("192."));

            if (ipAddress != null)
            {
                //Console.WriteLine("LAN IP Address: " + ipAddress.ToString());
                return ipAddress.ToString();
            }
            else
            {
                Log("LAN IP Address not found.");
                return null;
            }
        }

        public static bool PortAvailable(string port)
        {
            bool portAvailable = true;
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();

            foreach (IPEndPoint endpoint in tcpConnInfoArray)
            {
                if (endpoint.Port.ToString() == port)
                {
                    portAvailable = false;
                    break;
                }
            }

            return portAvailable;
        }

        internal static void InstallContextMenu()
        {
            string contextRegRoot = "Software\\Classes\\Directory\\Background\\shell";
            RegistryKey key = Registry.CurrentUser.OpenSubKey(contextRegRoot, true);

            // add folder if missing
            if (key == null)
            {
                key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Directory\Background\Shell");
            }

            if (key != null)
            {
                var appName = MainWindow.appname;
                key.CreateSubKey(appName);

                key = key.OpenSubKey(appName, true);
                key.SetValue("", "Start " + appName + " here");
                key.SetValue("Icon", "\"" + Process.GetCurrentProcess().MainModule.FileName + "\"");

                key.CreateSubKey("command");
                key = key.OpenSubKey("command", true);
                var executeString = "\"" + Process.GetCurrentProcess().MainModule.FileName + "\"";
                // TODO add port
                executeString += " \"%V\"";
                key.SetValue("", executeString);
                Log("Installed context menu item!");
            }
            else
            {
                Log("Error> Cannot find registry key: " + contextRegRoot, ConsoleColor.Red);
            }
        }

        static bool IsInstalledInRegistry()
        {
            string contextRegRoot = "Software\\Classes\\Directory\\Background\\shell";
            RegistryKey key = Registry.CurrentUser.OpenSubKey(contextRegRoot, true);
            if (key != null)
            {
                var appName = "webs";
                RegistryKey appKey = Registry.CurrentUser.OpenSubKey(contextRegRoot + "\\" + appName, false);
                if (appKey != null)
                {
                    return true;
                }
            }
            return false;
        }

        public static void UninstallContextMenu()
        {
            string contextRegRoot = "Software\\Classes\\Directory\\Background\\shell";
            RegistryKey key = Registry.CurrentUser.OpenSubKey(contextRegRoot, true);
            if (key != null)
            {
                var appName = MainWindow.appname;
                RegistryKey appKey = Registry.CurrentUser.OpenSubKey(contextRegRoot + "\\" + appName, false);
                if (appKey != null)
                {
                    key.DeleteSubKeyTree(appName);
                    Log("Removed context menu item!");
                }
                else
                {
                    //Console.WriteLine("Nothing to uninstall..");
                }
            }
            else
            {
                Log("Error> Cannot find registry key: " + contextRegRoot, ConsoleColor.Red);
            }
        }

        internal static void LaunchBrowser(string url)
        {
            Log("Launching browser: " + url);
            try
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log("Error launching browser: " + ex.Message, ConsoleColor.Red);
            }
        }

        internal static void RestartAsAdmin(string[] args)
        {
            try
            {
                // Get the path to the current executable
                string exePath = Process.GetCurrentProcess().MainModule.FileName;

                // Create a new process with elevated permissions
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.UseShellExecute = true;
                startInfo.WorkingDirectory = Environment.CurrentDirectory;
                startInfo.FileName = exePath;
                startInfo.Arguments = string.Join(" ", args); // Pass the same arguments
                startInfo.Verb = "runas"; // Run as administrator

                try
                {
                    Process.Start(startInfo);
                }
                catch (Exception ex)
                {
                    Tools.Log("Error restarting as administrator: " + ex.Message, ConsoleColor.Red);
                    Environment.Exit(0);
                    throw;
                }

                // Exit the current process
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Log("Error restarting as administrator: " + ex.Message, ConsoleColor.Red);
            }
        }

        public static bool IsUserAnAdmin()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            // Check for the admin SIDs
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static string GetExePath()
        {
            return Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
        }

        public static void PrintHelpAndExit(bool waitEnter = false)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("");
            Console.WriteLine("--- Optional Parameters (in this order) ------------------------------------------");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("folder_to_host\tExample: c\\websites\\test1");
            Console.WriteLine("http_port\tDefault is 8080");
            Console.WriteLine("https_port\tDefault is 4443. Only used if running as Admin");
            Console.WriteLine("admin\tMust be used, if want to host using IP address and have outside connections");
            Console.WriteLine("nobrowser\tDon't open browser automatically");
            Console.WriteLine("ignoreconfig\tWon't read local config.ini");
            Console.WriteLine("https\tEnable https connection (requires running as an Admin)");
            Console.WriteLine("");
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("--- Setup Parameters (in any order) ----------------------------------------------");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("install\tInstalls Explorer context menu to start hosting is folder");
            Console.WriteLine("uninstall\tRemoves Explorer context menu to start hosting is folder");
            Console.WriteLine("addpath\tAdds webs.exe folder into user PATH environment variable");
            Console.WriteLine("removepath\tRemoves webs.exe folder from user PATH environment variable");
            Console.WriteLine("");
            Console.WriteLine("? /? -?\tDisplay this help");
            Console.ForegroundColor = ConsoleColor.White;
            if (waitEnter == true) Console.ReadLine();
            Environment.Exit(0);
        }

        public static void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine("\n ::::: simple local webserver :::::");
            Console.ForegroundColor = ConsoleColor.Magenta;
            string asciiArt = @" ██╗    ██╗███████╗██████╗ ███████╗
 ██║    ██║██╔════╝██╔══██╗██╔════╝
 ██║ █╗ ██║█████╗  ██████╔╝███████╗
 ██║███╗██║██╔══╝  ██╔══██╗╚════██║
 ╚███╔███╔╝███████╗██████╔╝███████║
  ╚══╝╚══╝ ╚══════╝╚═════╝ ╚══════╝";
            Console.WriteLine(asciiArt);
            Console.ForegroundColor = ConsoleColor.DarkBlue;
            Console.WriteLine(" https://github.com/unitycoder/webs");
            Console.ForegroundColor = ConsoleColor.White;
        }

    } // class
} // namespace webs