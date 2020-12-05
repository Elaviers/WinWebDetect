using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace WinWebDetect
{
    class ChangeDetector
    {
        private class Tracker
        {
            public class SubTracker
            {
                public int desiredState;
                private readonly string text;
                public string warn;

                public SubTracker(string text)
                {
                    this.desiredState = -1;
                    this.text = text;
                }

                public struct CheckResult
                {
                    public bool notify;

                    public string message;
                    public ConsoleColor messageColour;
                }

                //Returns true if desired state is met
                public void Check(string content, ref CheckResult outResult)
                {
                    int state = content.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0;
                    if (desiredState < 0) desiredState = state == 0 ? 1 : 0;

                    if (warn != null)
                    {
                        outResult.notify = false;

                        if (state == desiredState)
                        {
                            outResult.message = warn;
                            outResult.messageColour = ConsoleColor.Yellow;
                        }
                        else
                        {
                            outResult.message = string.Empty;
                        }
                    }
                    else
                    {
                        outResult.notify = state == desiredState;
                        outResult.message = "▓";
                        outResult.messageColour = state != 0 ? ConsoleColor.Green : ConsoleColor.Red;
                    }
                }
            }

            public string name;
            public readonly string url;
            private readonly string cookieString;

            private readonly List<SubTracker> subtrackers;

            private SubTracker.CheckResult[] _subtrackerResults;
            public SubTracker.CheckResult[] SubtrackerResults { get => _subtrackerResults; }

            public bool canNotify;

            public bool debug;

            private string _latestContent;
            public string LatestContent { get => _latestContent; }

            public Tracker(string url)
            {
                this.name = string.Empty;
                this.url = url;
                this.cookieString = CookieReader.GetCookieString(url);
                this.subtrackers = new List<SubTracker>();
                this.canNotify = true;
                this.debug = false;
            }

            public enum CheckResult
            {
                IS_DESIRED_STATE,
                IS_NOT_DESIRED_STATE,
                FAIL_TOO_MANY_REQUESTS,
                FAIL
            }

            public CheckResult Check(HttpClient web)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
                request.Headers.Add("Connection", "close");
                request.Headers.Add("Upgrade-Insecure-Requests", "1");
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/87.0.4280.67 Safari/537.36 Edg/87.0.664.52");
                request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
                request.Headers.Add("Sec-Fetch-Site", "none");
                request.Headers.Add("Sec-Fetch-Mode", "navigate");
                request.Headers.Add("Sec-Fetch-User", "?1");
                request.Headers.Add("Sec-Fetch-Dest", "document");
                request.Headers.Add("Accept-Encoding", "gzip, deflate");
                request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
                request.Headers.Add("Cookie", cookieString);

                HttpResponseMessage response = web.SendAsync(request, HttpCompletionOption.ResponseContentRead).Result;

                if (response.IsSuccessStatusCode)
                {
                    _latestContent = response.Content.ReadAsStringAsync().Result;

                    bool differsFromInitial = false;
                    if (_subtrackerResults == null || _subtrackerResults.Length != subtrackers.Count)
                        _subtrackerResults = new SubTracker.CheckResult[subtrackers.Count];

                    for (int i = 0; i < subtrackers.Count; ++i)
                    {
                        subtrackers[i].Check(_latestContent, ref _subtrackerResults[i]);

                        if (_subtrackerResults[i].notify)
                            differsFromInitial = true;
                    }

                    return differsFromInitial ? CheckResult.IS_DESIRED_STATE : CheckResult.IS_NOT_DESIRED_STATE;
                }

                _subtrackerResults = null;
                
                if ((int)response.StatusCode == 429)
                    return CheckResult.FAIL_TOO_MANY_REQUESTS;

                return CheckResult.FAIL;
            }

            public void Add(string text)
            {
                subtrackers.Add(new SubTracker(text));
            }

            public void TrySetSTDesiredState(bool desiredState)
            {
                if (subtrackers.Count > 0)
                    subtrackers[subtrackers.Count - 1].desiredState = desiredState ? 1 : 0;
            }

            public void TrySetSTWarn(string warn)
            {
                if (subtrackers.Count > 0)
                    subtrackers[subtrackers.Count - 1].warn = warn;
            }
        }

        private readonly List<Tracker> trackers = new List<Tracker>();

        public delegate void Notify(string name, string url);

        //notify args are name and url
        public void Check(HttpClient client, int msInterval, bool asyncMode, Notify notify)
        {
            Task<Tracker.CheckResult>[] tasks = asyncMode ? new Task<Tracker.CheckResult>[trackers.Count] : null;

            if (asyncMode)
            {
                for (int i = 0; i < trackers.Count; ++i)
                {
                    Tracker e = trackers[i];
                    tasks[i] = Task.Run(() => e.Check(client));
                }
            }

            for (int i = 0; i < trackers.Count; ++i)
            {
                Tracker e = trackers[i];
                Console.ForegroundColor = ConsoleColor.White;
                Console.Out.Write($"{(e.name.Length > 0 ? e.name : e.url)} ");

                Tracker.CheckResult checkResult = asyncMode ? tasks[i].Result : e.Check(client);

                if (e.SubtrackerResults != null)
                {
                    foreach (var result in e.SubtrackerResults)
                        if (result.message.Length > 0)
                        {
                            Console.ForegroundColor = result.messageColour;
                            Console.Out.Write(result.message);
                        }

                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Out.Write('|');
                }

                if (checkResult == Tracker.CheckResult.IS_DESIRED_STATE)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Out.Write("█ ");

                    if (e.canNotify)
                    {
                        e.canNotify = false;

                        if (e.debug)
                        {
                            System.IO.File.WriteAllText("DEBUG.TXT", e.LatestContent);
                            Console.Out.Write("(DEBUG.TXT WRITTEN)");
                        }

                        notify(e.name, e.url);
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    switch(checkResult)
                    {
                        case Tracker.CheckResult.IS_NOT_DESIRED_STATE:
                            Console.Out.Write("█ ");
                            break;

                        case Tracker.CheckResult.FAIL_TOO_MANY_REQUESTS:
                            Console.Out.Write("(TOO MANY REQUESTS)");
                            break;

                        default:
                            Console.Out.Write("FAIL");
                            break;
                    }

                    e.canNotify = true;
                }

                if (!asyncMode)
                    System.Threading.Thread.Sleep(msInterval);
            }

            if (asyncMode)
                System.Threading.Thread.Sleep(msInterval);
        }

        public void AddTracker(string url)
        {
            trackers.Add(new Tracker(url));
        }

        //Adds a check to the latest tracker
        public void AddCheck(string check)
        {
            trackers[trackers.Count - 1].Add(check);
        }

        //Attempts to set name of latest tracker
        public void TrySetTrackerName(string name)
        {
            if (trackers.Count > 0)
                trackers[trackers.Count - 1].name = name;
        }

        public void TrySetTrackerDebug(bool debug)
        {
            if (trackers.Count > 0)
                trackers[trackers.Count - 1].debug = debug;
        }

        //Attempts to set desired state of the latest tracker's latest subtracker
        public void TrySetTrackerSTDesiredState(bool desiredState)
        {
            if (trackers.Count > 0)
                trackers[trackers.Count - 1].TrySetSTDesiredState(desiredState);
        }

        public void TrySetTrackerSTWarn(string warn)
        {
            if (trackers.Count > 0)
                trackers[trackers.Count - 1].TrySetSTWarn(warn);
        }

        public int URLCount { get { return trackers.Count; } }
    }
}
