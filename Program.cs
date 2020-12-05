using System;
using System.Collections.Generic;
using System.Media;
using System.Windows.Forms;
using System.Net.Http;
using System.IO;
using System.Text.RegularExpressions;

namespace WinWebDetect
{
    class Program
    {
        class Entry
        {
            public string url;
            public List<string> changestrings;

            public Entry(string url)
            {
                this.url = url;
                this.changestrings = new List<string>();
            }
        }

        public struct Settings
        {
            public bool newurl;

            public float interval;
            public bool asyncMode;
            public bool autoOpen;

            public Dictionary<string, string> profiles;
        }

        private static void ProcessArgs(int depth, string argstring, ChangeDetector cd, ref Settings settings)
        {
            MatchCollection m = Regex.Matches(argstring, @"""((?:\\.|[^""])*)""|\S+"); //Match quoted strings and arguments, ignore escaped quotes
            string[] args = new string[m.Count];

            for (int argIndex = 0; argIndex < args.Length; ++argIndex)
            {
                args[argIndex] = (m[argIndex].Groups[1].Length > 0 ? m[argIndex].Groups[1].Value : m[argIndex].Value)
                    .Replace("\\\"", "\"");
            }

            ProcessArgs(depth, args, cd, ref settings);
        }

        private static void ProcessArgs(int depth, string[] args, ChangeDetector cd, ref Settings settings)
        {
            string indent = string.Empty;
            for (int i = 0; i < depth; ++i)
                indent += '\t';

            string flag = null;
            for (int i = 0; i < args.Length; ++i)
            {
                string arg = args[i];

                if (arg[0] == '/')
                {
                    if (arg.Length == 1)
                    {
                        settings.newurl = true;
                        continue;
                    }

                    flag = arg.Substring(1);

                    if (flag.Equals("?"))
                    {
                        if (depth == 0)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("WinWebDetect");
                            Console.ForegroundColor = ConsoleColor.Gray;
                            Console.WriteLine("Now 100% legal-ish");

                            Console.ForegroundColor = ConsoleColor.White;
                            Console.WriteLine(@"
Usage:
    WinWebDetect [/i INTERVAL] URL [TRACKERS...] [/ URL [TRACKERS...] [/ URL [TRACKERS...] - ...]]
    
    Where URL is a URL to request and TRACKERS are strings to look for in the request

Flags:
    FLAG            ARGS    DESCRIPTION
    ___________________________________
    /               NONE            Seperator
    /a, /async      NONE            Query each URL asynchronously (at the same time)
    /b, /browser    STRING          Set which browser's cookies to use in http requests (""edge"", ""chrome"", or ""none"")
    /i, /interval   FLOAT           Amount of time between each check, in seconds

    /y              NONE            Automatically open url when change detected (popup box still appears)
    /s              NONE            Marks the desired state of the latest tracker as FALSE
    /S              NONE            Marks the desired state of the latest tracker as TRUE
    /n, /name       NONE            Sets name of the latest url
    /w, /warn       STRING          Marked tracker will warn if met desired state

    /d, /define     (NAME)=(ARGS)   Define a profile
    /p, /profile    STRING          Use a profile

Notes:
    any URL or TRACKER argument may instead be a file path
    Files are read the same way as arguments, with newlines also acting as seperators

    The program will attempt to read arguments from webdetect.txt if no URLs are provided");
                        }

                        return;
                    }
                    
                    if (flag.Equals("async", StringComparison.OrdinalIgnoreCase) || flag.Equals("a", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.asyncMode = true;
                        flag = null;
                        Console.WriteLine(indent + "Async mode enabled");
                    }
                    else if (flag.Equals("y", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.autoOpen = true;
                        flag = null;
                        Console.WriteLine(indent + "Auto-open enabled");
                    }
                    else if (flag.Equals("s"))
                    {
                        cd.TrySetTrackerSTDesiredState(false);
                        flag = null;
                        Console.WriteLine(indent + "Desired state->FALSE");
                    }
                    else if (flag.Equals("S"))
                    {
                        cd.TrySetTrackerSTDesiredState(true);
                        flag = null;
                        Console.WriteLine(indent + "Desired state->TRUE");
                    }
                }
                else
                {
                    if (flag != null)
                    {
                        if (flag.Equals("browser", StringComparison.OrdinalIgnoreCase) || flag.Equals("b", StringComparison.OrdinalIgnoreCase))
                        {
                            CookieReader.CookieSource newSource = CookieReader.CookieSource.NONE;

                            if (arg.Equals("chrome", StringComparison.OrdinalIgnoreCase))
                                newSource = CookieReader.CookieSource.CHROME;
                            else if (arg.Equals("edge", StringComparison.OrdinalIgnoreCase))
                                newSource = CookieReader.CookieSource.EDGE_CHROMIUM;

                            CookieReader.SetCookieSource(newSource);
                            Console.WriteLine($"CookieSource->{newSource}");
                        }
                        else if (flag.Equals("interval", StringComparison.OrdinalIgnoreCase) || flag.Equals("i", StringComparison.OrdinalIgnoreCase))
                        {
                            float.TryParse(arg, out settings.interval);
                            Console.WriteLine(indent + $"Interval->{settings.interval}");
                        }
                        else if (flag.Equals("name", StringComparison.OrdinalIgnoreCase) || flag.Equals("n", StringComparison.OrdinalIgnoreCase))
                        {
                            cd.TrySetTrackerName(arg);
                            Console.WriteLine(indent + $"Name->{arg}");
                        }
                        else if (flag.Equals("warn", StringComparison.OrdinalIgnoreCase) || flag.Equals("w", StringComparison.OrdinalIgnoreCase))
                        {
                            cd.TrySetTrackerSTWarn(arg);
                            Console.WriteLine(indent + $"Warn->{arg}");
                        }
                        else if (flag.Equals("define", StringComparison.OrdinalIgnoreCase) || flag.Equals("d", StringComparison.OrdinalIgnoreCase))
                        {
                            int equals = arg.IndexOf('=');
                            if (equals > 0)
                            {
                                string name = arg.Substring(0, equals);
                                string def = arg.Substring(equals + 1);
                                Console.WriteLine(indent + $"Profile \"{name}\"->{def}");

                                settings.profiles[name] = def;
                            }
                            else
                                Console.WriteLine(indent + $"Invalid profile def \"{arg}\"");
                        }
                        else if (flag.Equals("profile", StringComparison.OrdinalIgnoreCase) || flag.Equals("p", StringComparison.OrdinalIgnoreCase))
                        {
                            if (settings.profiles.TryGetValue(arg, out string profile))
                            {
                                Console.WriteLine(indent + $"Applying profile \"{arg}\"...");
                                ProcessArgs(depth + 1, profile, cd, ref settings);
                            }
                            else
                            {
                                Console.WriteLine(indent + $"Profile \"{arg}\" not found!");
                            }
                        }
                        else
                        {
                            Console.WriteLine(indent + $"Unknown flag \"/{flag}\"!");
                        }

                        flag = null;
                    }
                    else
                    {
                        if (File.Exists(arg))
                        {
                            Console.WriteLine(indent + $"Reading arguments from \"{arg}\":");
                            string filetext = File.ReadAllText(arg);
                            if (filetext.Length > 0)   
                                ProcessArgs(depth + 1, filetext.Replace("\n", " / "), cd, ref settings); //Replace newlines with seperators
                        }
                        else if (settings.newurl)
                        {
                            if (!arg.Substring(0, 4).Equals("http", StringComparison.OrdinalIgnoreCase))
                            {
                                Console.Write(indent + $"\"{arg}\"->");
                                arg = "https://" + arg;
                                Console.WriteLine($"\"{arg}\"");
                            }

                            cd.AddTracker(arg);
                            settings.newurl = false;

                            Console.WriteLine(indent + $"New URL: {arg}");
                        }
                        else
                        {
                            cd.AddCheck(arg);

                            Console.WriteLine(indent + $"Tracker: {arg}");
                        }
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            const string defaultFile = "./webdetect.txt";

            try
            {
                CookieReader.SetCookieSource(CookieReader.CookieSource.CHROME);
                Console.WriteLine("Will use cookies from CHROME");
            }
            catch (Exception)
            {
                try
                {
                    CookieReader.SetCookieSource(CookieReader.CookieSource.EDGE_CHROMIUM);
                    Console.WriteLine("Will use cookies from EDGE_CHROMIUM");
                }
                catch (Exception)
                {
                    Console.WriteLine("CHROME or EDGE_CHROMIUM cookies not found.\nNo cookies will be used in http requests!");
                }
            }

            Settings settings = new Settings();
            settings.newurl = true;
            settings.interval = 1f;
            settings.asyncMode = false;
            settings.autoOpen = false;
            settings.profiles = new Dictionary<string, string>();

            ChangeDetector cd = new ChangeDetector();
            ProcessArgs(0, args, cd, ref settings);

            if (cd.URLCount == 0 && File.Exists(defaultFile))
                ProcessArgs(0, new string[1] { defaultFile }, cd, ref settings);

            if (cd.URLCount == 0)
            {
                Console.WriteLine("No URLS provided, exiting..");
                return;
            }

            SoundPlayer player = new SoundPlayer("./alert.wav");

            int msInterval = (int)(settings.interval * 1000);

            HttpClientHandler handler = new HttpClientHandler()
            {
                UseCookies = false,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };

            HttpClient client = new HttpClient(handler);
            while (true)
            {
                var date = DateTime.Now;
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Out.Write($"{date.Month:d02}/{date.Day:d02} {date.Hour:d02}:{date.Minute:d02}:{date.Second:d02} ");

                cd.Check(client, msInterval, settings.asyncMode,
                    (string name, string url) => {
                        System.Threading.Tasks.Task.Run(() =>
                        {
                            if (settings.autoOpen)
                                System.Diagnostics.Process.Start(url);

                            player.PlayLooping();

                            string fname = name.Length > 0 ? $"\"{name} ({url})\"" : url;
                            var result = MessageBox.Show(
                                $"{fname} has been updated.\nGo there now?",
                                $"Change detected for {fname}",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1, (MessageBoxOptions)0x40000 /*topmost*/);

                            player.Stop();

                            if (result == DialogResult.Yes)
                                System.Diagnostics.Process.Start(url);
                        });
                });

                Console.Out.WriteLine();
            }
        }
    }
}
