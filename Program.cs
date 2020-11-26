using System;
using System.Collections.Generic;
using System.Media;
using System.Windows.Forms;
using System.Net.Http;
using System.IO;
using System.Text.RegularExpressions;

namespace WebChangeDetect
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

        private static void ProcessArgs(int depth, string[] args, ChangeDetector cd, ref float interval)
        {
            string indent = "";
            for (int i = 0; i < depth; ++i)
                indent += '\t';

            bool newurl = true;
            string flag = null;
            for (int i = 0; i < args.Length; ++i)
            {
                string arg = args[i];

                if (arg[0] == '/')
                {
                    if (arg.Length == 1)
                    {
                        newurl = true;
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
    /                   Seperator
    /i, /interval       Amount of time between each check
    /s                  Marks the desired state of the current tracker as FALSE
    /S                  Marks the desired state of the current tracker as TRUE
    /n, /name           Name of the current url

Notes:
    any URL or TRACKER argument may instead be a file path
    Files are read the same way as arguments, with newlines also acting as seperators

    The program will attempt to read arguments from webdetect.txt if no URLs are provided");
                        }

                        return;
                    }
                    if (flag.Equals("s"))
                    {
                        cd.TrySetTrackerSTState(true);
                        flag = null;
                        Console.WriteLine(indent + "Desired state->FALSE");
                    }
                    else if (flag.Equals("S"))
                    {
                        cd.TrySetTrackerSTState(false);
                        flag = null;
                        Console.WriteLine(indent + "Desired state->TRUE");
                    }
                }
                else
                {
                    if (flag != null)
                    {
                        if (flag.Equals("interval", StringComparison.OrdinalIgnoreCase) || flag.Equals("i", StringComparison.OrdinalIgnoreCase))
                        {
                            float.TryParse(arg, out interval);
                            flag = null;
                            Console.WriteLine(indent + $"Interval->{interval}");
                        }
                        else if (flag.Equals("name", StringComparison.OrdinalIgnoreCase) || flag.Equals("n", StringComparison.OrdinalIgnoreCase))
                        {
                            cd.TrySetTrackerName(arg);
                            flag = null;
                            Console.WriteLine(indent + $"Name->{arg}");
                        }
                        else
                        {
                            flag = null;
                            Console.WriteLine(indent + $"Unknown flag \"/{flag}\"!");
                        }
                    }
                    else
                    {
                        if (File.Exists(arg))
                        {
                            Console.WriteLine(indent + $"Reading arguments from \"{arg}\":");
                            string filetext = File.ReadAllText(arg);

                            if (filetext.Length > 0)
                            {
                                filetext = filetext.Replace("\n", " / "); //Replace newlines with seperators

                                MatchCollection m = Regex.Matches(filetext, @"""((?:\\.|[^""])*)""|\S+");
                                string[] fileargs = new string[m.Count];

                                for (int argIndex = 0; argIndex < fileargs.Length; ++argIndex)
                                {
                                    fileargs[argIndex] = (m[argIndex].Groups[1].Length > 0 ? m[argIndex].Groups[1].Value : m[argIndex].Value)
                                        .Replace("\\\"", "\"");
                                }

                                ProcessArgs(depth + 1, fileargs, cd, ref interval);
                            }
                        }
                        else if (newurl)
                        {
                            if (!arg.Substring(0, 4).Equals("http", StringComparison.OrdinalIgnoreCase))
                            {
                                Console.Write(indent + $"\"{arg}\"->");
                                arg = "https://" + arg;
                                Console.WriteLine($"\"{arg}\"");
                            }

                            cd.AddTracker(arg);
                            newurl = false;

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

            ChangeDetector cd = new ChangeDetector();
            float interval = 1f;
            ProcessArgs(0, args, cd, ref interval);

            if (cd.IsEmpty && File.Exists(defaultFile))
                ProcessArgs(0, new string[1] { defaultFile }, cd, ref interval);

            if (cd.IsEmpty)
            {
                Console.WriteLine("No URLS provided, exiting..");
                return;
            }

            SoundPlayer player = new SoundPlayer("./alert.wav");

            int msInterval = (int)(interval * 1000);
            HttpClient client = new HttpClient();
            while (true)
            {
                var date = DateTime.Now;
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Out.Write($"{date.Month:d02}/{date.Day:d02} {date.Hour:d02}:{date.Minute:d02}:{date.Second:d02} ");

                cd.Check(client, msInterval,
                    (string name, string url) => {
                        player.PlayLooping();

                        string fname = name.Length > 0 ? $"\"{name} ({url})\"" : url;

                        var result = MessageBox.Show(
                            $"{fname} has been updated.\nGo there now?",
                            $"Change detected for {fname}",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);

                        player.Stop();

                        if (result == DialogResult.Yes)
                        {
                            System.Diagnostics.Process.Start(url);
                        }
                    });

                Console.Out.WriteLine();
            }
        }
    }
}
