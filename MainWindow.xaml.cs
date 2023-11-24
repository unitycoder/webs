using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;

namespace webs
{
    public partial class MainWindow : Window
    {
        public static string appname = "webs";
        //static readonly string rootFolder = AppDomain.CurrentDomain.BaseDirectory;
        static string rootFolder = "";

        // allow console output from WPF application https://stackoverflow.com/a/7559336/5452781
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AttachConsole(uint dwProcessId);
        const uint ATTACH_PARENT_PROCESS = 0x0ffffffff;

        // detach from console, otherwise file is locked https://stackoverflow.com/a/29572349/5452781
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool FreeConsole();

        Thread workerThread;
        static bool abort = false;
        public static MainWindow mainWindowStatic;
        bool isInitialiazing = true;
        private static bool allowExternalConnections;

        public MainWindow()
        {
            InitializeComponent();
            mainWindowStatic = this;
            Main();
        }

        private void Main()
        {
            // check cmdline args
            string[] args = Environment.GetCommandLineArgs();

            Tools.ForceDotCultureSeparator();

            if (args.Length > 1)
            {
                AttachConsole(ATTACH_PARENT_PROCESS);

                Tools.PrintBanner();

                var parameters = ArgParser.Parse(args, AppDomain.CurrentDomain.BaseDirectory);

                if (parameters.errors.Count == 0)
                {
                    // handle install and other commands
                    if (parameters.installContextMenu == true) Tools.InstallContextMenu();
                    if (parameters.uninstallContextMenu == true) Tools.UninstallContextMenu();
                    if (parameters.addPath == true) Tools.ModifyUserEnvPATH(add: true);
                    if (parameters.removePath == true) Tools.ModifyUserEnvPATH(add: false);

                    // do we have folder
                    if (string.IsNullOrEmpty(parameters.rootFolder) == false)
                    {
                        StartServer(parameters);

                        // wait for exit
                        Console.ReadLine();

                        // NOTE need to wait readline inside VS manually (for easier testing), only if inside VS
                        if (Debugger.IsAttached)
                        {
                            // Start a new instance of cmd.exe (the Windows command prompt)
                            Process cmdProcess = new Process();
                            cmdProcess.StartInfo.FileName = "cmd.exe";
                            cmdProcess.StartInfo.RedirectStandardInput = true;
                            cmdProcess.StartInfo.RedirectStandardOutput = true;
                            cmdProcess.StartInfo.UseShellExecute = false;
                            cmdProcess.StartInfo.CreateNoWindow = false;

                            // Start the process
                            cmdProcess.Start();

                            // Get the StandardInput stream and StandardOutput stream
                            StreamWriter cmdStreamWriter = cmdProcess.StandardInput;
                            StreamReader cmdStreamReader = cmdProcess.StandardOutput;

                            // You can send commands to the console like this:
                            cmdStreamWriter.WriteLine("TestWriteLine");

                            // Wait for user input in the console
                            Console.WriteLine("Press Enter to continue...");
                            cmdStreamReader.ReadLine(); // Wait for user input

                            cmdProcess.WaitForExit(); // Wait for the process to exit
                            cmdStreamWriter.Close(); // Close the input stream to indicate that we're done writing
                        }
                    }
                }

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Exited.");
                FreeConsole();
                Environment.Exit(0);
            }

            // regular WPF gui starts here
            this.Title = appname;

            // disable accesskeys without alt
            CoreCompatibilityPreferences.IsAltKeyRequiredInAccessKeyDefaultScope = true;

            //LoadSettings();
        }

        private static void StartServer(Parameters args)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{args.HTTP_port}/");

            rootFolder = args.rootFolder;

            if (args.runAsAdmin == true)
            {
                // then allow external connections
                allowExternalConnections = true;
                Tools.Log("The application is running as an administrator. External connections are allowed!", ConsoleColor.Yellow);
                // NOTE using hostname ipaddress requires admin rights
                var ipAddress = Tools.GetIpAddress();
                if (string.IsNullOrEmpty(ipAddress.ToString()) == false)
                {
                    // NOTE this could be needed sometimes, maybe this instead of localhost, if admin?
                    //listener.Prefixes.Add($"http://{ipAddress}:{args.HTTP_port}/");

                    // NOTE you can enable HTTPS here, if you have https setup done https://gist.github.com/unitycoder/ec217d20eecc2dfaf8d316acd8c3c5c5

                    if (args.runAsAdmin == true) listener.Prefixes.Add($"https://{ipAddress}:{args.HTTPS_port}/");
                }

                // NOTE or can add localhost with https
                //listener.Prefixes.Add($"https://localhost:4443/");
            }
            else
            {
                Tools.Log("The application is not running as an administrator.", ConsoleColor.Cyan);
            }

            foreach (string prefix in listener.Prefixes)
            {
                Tools.Log("Listening: " + prefix, ConsoleColor.Green);
            }

            listener.Start();
            listener.BeginGetContext(RequestHandler, listener);

            Tools.Log("-----------------------------------------", ConsoleColor.DarkCyan);

            // open browser if wanted
            if (args.noBrowser == false)
            {
                foreach (var item in listener.Prefixes)
                {
                    Tools.LaunchBrowser(item);
                }
            }
        }

        static void RequestHandler(IAsyncResult result)
        {
            HttpListener listener = (HttpListener)result.AsyncState;
            HttpListenerContext context = listener.EndGetContext(result);

            // Start accepting the next request asynchronously
            listener.BeginGetContext(RequestHandler, listener);

            try
            {
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                // set headers to disable caching
                response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                response.Headers["Pragma"] = "no-cache";
                response.Headers["Expires"] = "0";

                string path = Uri.UnescapeDataString(context.Request.Url.LocalPath);

                if (path == "/")
                {
                    path = "/index.html";
                }

                if (Path.GetExtension(path) == ".html" || path.EndsWith(".js") || path.EndsWith(".js.gz") || path.EndsWith(".js.br"))
                {
                    response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
                    response.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";
                    response.Headers["Cross-Origin-Resource-Policy"] = "cross-origin";
                }

                // unity webgl support
                if (Path.GetExtension(path) == ".gz")
                {
                    response.AddHeader("Content-Encoding", "gzip");
                }
                else if (Path.GetExtension(path) == ".br")
                {
                    response.AddHeader("Content-Encoding", "br");
                }

                if (context.Request.Headers.Get("Range") != null)
                {
                    response.AddHeader("Accept-Ranges", "bytes");
                }

                if (path.EndsWith(".wasm") || path.EndsWith(".wasm.gz") || path.EndsWith(".wasm.br"))
                {
                    response.ContentType = "application/wasm";
                }
                else if (path.EndsWith(".js") || path.EndsWith(".js.gz") || path.EndsWith(".js.br"))
                {
                    response.ContentType = "application/javascript";
                }
                else if (path.EndsWith(".data.gz"))
                {
                    response.ContentType = "application/gzip";
                }
                else if (path.EndsWith(".data") || path.EndsWith(".data.br"))
                {
                    response.ContentType = "application/octet-stream";
                }

                string page = rootFolder + path;
                string msg = null;

                // this allows only local access
                if (allowExternalConnections == false && context.Request.IsLocal == false)
                {
                    Tools.Log("Forbidden.", ConsoleColor.Red);
                    msg = "<html><body>403 Forbidden</body></html>";
                    response.StatusCode = 403;
                }
                else if (!File.Exists(page))
                {
                    Tools.Log("Not found: " + page, ConsoleColor.Red);
                    msg = "<html><body>404 Not found</body></html>";
                    response.StatusCode = 404;
                }
                else
                {
                    // display client ip address and request info
                    Tools.Log(context.Request.RemoteEndPoint.Address + " < " + path + (response.ContentType != null ? " (" + response.ContentType + ")" : ""));

                    using (FileStream fileStream = File.Open(page, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (BinaryReader reader = new BinaryReader(fileStream))
                    {
                        response.ContentLength64 = fileStream.Length;

                        byte[] buffer = new byte[4096];
                        int bytesRead;

                        try
                        {
                            while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                response.OutputStream.Write(buffer, 0, bytesRead);
                            }
                        }
                        catch (Exception ex)
                        {
                            Tools.Log("Error reading file: " + ex, ConsoleColor.Yellow);
                        }
                    }
                }

                if (msg != null)
                {
                    byte[] buffer = Encoding.UTF8.GetBytes(msg);
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }

                response.Close();
            }
            catch (Exception)
            {

                throw;
            }
        } // RequestHandler
    } // class
} // namespace
