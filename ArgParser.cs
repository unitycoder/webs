using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace webs
{
    public class ArgParser
    {
        const char argValueSeparator = '=';

        [DllImport("shell32.dll", SetLastError = true)]
        static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);

        [DllImport("kernel32.dll")]
        static extern IntPtr LocalFree(IntPtr hMem);

        static string[] SplitArgs(string unsplitArgumentLine)
        {
            int numberOfArgs;
            IntPtr ptrToSplitArgs;
            string[] splitArgs;

            ptrToSplitArgs = CommandLineToArgvW(unsplitArgumentLine, out numberOfArgs);

            // CommandLineToArgvW returns NULL upon failure.
            if (ptrToSplitArgs == IntPtr.Zero) throw new ArgumentException("Unable to split argument.", new Win32Exception());

            // Make sure the memory ptrToSplitArgs to is freed, even upon failure.
            try
            {
                splitArgs = new string[numberOfArgs];

                // ptrToSplitArgs is an array of pointers to null terminated Unicode strings.
                // Copy each of these strings into our split argument array.
                for (int i = 0; i < numberOfArgs; i++)
                {
                    splitArgs[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(ptrToSplitArgs, i * IntPtr.Size));
                }

                return splitArgs;
            }
            finally
            {
                // Free memory obtained by CommandLineToArgW.
                LocalFree(ptrToSplitArgs);
            }
        }

        static string Reverse(string s)
        {
            char[] charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }

        static string GetEscapedCommandLine()
        {
            StringBuilder sb = new StringBuilder();
            bool gotQuote = false;
            foreach (var c in Environment.CommandLine.Reverse())
            {
                if (c == '"')
                    gotQuote = true;
                else if (gotQuote && c == '\\')
                {
                    // double it
                    sb.Append('\\');
                }
                else
                    gotQuote = false;

                sb.Append(c);
            }

            return Reverse(sb.ToString());
        }

        public static Parameters Parse(string[] args, string rootFolder)
        {
            Parameters parameters = new Parameters();

            // if there are any errors, they are added to this list, then importing is aborted after parsing arguments
            List<string> errors = new List<string>();

            string[] originalArgs = args;

            // handle manual args (null is default args, not used)
            //if (args == null) args = SplitArgs(GetEscapedCommandLine()).Skip(1).ToArray();

            // parse commandline arguments
            if (args != null && args.Length > 0)
            {
                // folder backslash quote fix https://stackoverflow.com/a/9288040/5452781
                for (int i = 0; i < args.Length; i++)
                {
                    Console.WriteLine(args[i]);

                    // help
                    if (args[i].Contains("?") || args[i].ToLower() == "help") Tools.PrintHelpAndExit();

                    // check if arg is a folder
                    if (Directory.Exists(args[i]))
                    {
                        parameters.rootFolder = args[i];
                        continue;
                    }

                    int portNumber;
                    if (int.TryParse(args[i], out portNumber))
                    {
                        if (parameters.defaultHTTP == true)
                        {
                            parameters.HTTP_port = portNumber;
                            parameters.defaultHTTP = false;
                        }
                        else if (parameters.defaultHTTPS == true)
                        {
                            parameters.HTTPS_port = portNumber;
                            parameters.defaultHTTPS = false;
                        }
                        else
                        {
                            errors.Add("Too many ports specified");
                        }
                        continue;
                    }

                    // restart as admin, if not admin yet
                    if (args[i].ToLower() == "admin")
                    {
                        // check if admin already
                        bool isAdmin = Tools.IsUserAnAdmin();
                        if (isAdmin == true)
                        {
                            Tools.Log("Already running as an admin..", ConsoleColor.Yellow);
                        }
                        else
                        {
                            Tools.RestartAsAdmin(originalArgs);
                        }

                        parameters.runAsAdmin = true;
                        continue;
                    }

                    // dont open browser automatically
                    if (args[i].ToLower() == "nobrowser")
                    {
                        parameters.noBrowser = true;
                        continue;
                    }            
                    
                    // enable https
                    if (args[i].ToLower() == "https")
                    {
                        parameters.https = true;
                        continue;
                    }

                    // ignore local config.ini file
                    if (args[i].ToLower() == "ignoreconfig")
                    {
                        parameters.ignoreConfig = true;
                        continue;
                    }

                    if (args[i].ToLower() == "install")
                    {
                        if (parameters.uninstall == true)
                        {
                            errors.Add("Cannot install and uninstall at the same time");
                        }
                        parameters.installContextMenu = true;
                        continue;
                    }

                    if (args[i].ToLower() == "addPath")
                    {
                        if (parameters.removepath == true)
                        {
                            errors.Add("Cannot add and remove path at the same time");
                        }
                        parameters.addPath = true;
                        continue;
                    }

                    if (args[i].ToLower() == "uninstall")
                    {
                        if (parameters.installContextMenu == true)
                        {
                            errors.Add("Cannot install and uninstall at the same time");
                        }
                        parameters.uninstall = true;
                        continue;
                    }

                    if (args[i].ToLower() == "removepath")
                    {
                        if (parameters.addPath == true)
                        {
                            errors.Add("Cannot add and remove path at the same time");
                        }
                        parameters.removepath = true;
                        continue;
                    }

                } // for args
            }
            else // if no commandline args
            {
                Tools.PrintHelpAndExit(waitEnter: true);
            }

            // show errors
            if (errors.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\nErrors found:");
                Console.ForegroundColor = ConsoleColor.Red;
                for (int i = 0; i < errors.Count; i++)
                {
                    Console.WriteLine(i + "> " + errors[i]);
                }
                Console.ForegroundColor = ConsoleColor.White;
                parameters.errors = errors;
            }

            // return always, but note that we might have errors
            return parameters;
        }

    } // class
} // namespace
