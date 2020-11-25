using System;
using System.Collections.Generic;
using System.Media;
using System.Windows.Forms;
using System.Net.Http;

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

        static void Main(string[] args)
        {
            /*
            MatchCollection m = Regex.Matches(argstring, @"""((?:\\.|[^""])*)""|\S+");
            
            for (int i = 0; i < m.Count; ++i)
            {
                string arg = m[i].Groups[1].Length > 0 ? m[i].Groups[1].Value : m[i].Value;
            */

            float interval = 60f;
            ChangeDetector cd = new ChangeDetector();
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
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("WinWebDetect");
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine("Now 100% legal-ish");

                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine(
@"
Usage:
    WinWebDetect [/i INTERVAL] URL [TRACKERS...] [/ URL [TRACKERS...] [/ URL [TRACKERS...] - ...]]
    
    Where URL is a URL to request and TRACKERS are strings to look for in the request
    

Flags:
    /i, /interval       Amount of time between each check
    /s                  Marks the desired state of the current tracker as FALSE
    /S                  Marks the desired state of the current tracker as TRUE
    /n, /name           Name of the current url");


                        return;
                    }
                    if (flag.Equals("s"))
                    {
                        cd.TrySetTrackerSTState(true);
                        flag = null;
                    }
                    else if (flag.Equals("S"))
                    {
                        cd.TrySetTrackerSTState(false);
                        flag = null;
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
                        }
                        else if (flag.Equals("name", StringComparison.OrdinalIgnoreCase) || flag.Equals("n", StringComparison.OrdinalIgnoreCase))
                        {
                            cd.TrySetTrackerName(arg);
                            flag = null;
                        }
                        else
                            flag = null;
                    }
                    else
                    {
                        if (newurl)
                        {
                            if (!arg.Substring(0, 4).Equals("http", StringComparison.OrdinalIgnoreCase))
                            {
                                Console.Write($"\"{arg}\"->");
                                arg = "https://" + arg;
                                Console.WriteLine($"\"{arg}\"");
                            }

                            cd.AddTracker(arg);
                            newurl = false;
                        }
                        else
                            cd.AddCheck(arg);
                    }
                }
            }

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

                cd.Check(client, 
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
                
                System.Threading.Thread.Sleep(msInterval);
            }
        }
    }
}
