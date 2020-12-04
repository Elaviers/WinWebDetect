using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace WebChangeDetect
{
    class ChangeDetector
    {
        private class Tracker
        {
            private class SubTracker
            {
                public int initialState;
                private readonly string text;
                
                public SubTracker(string text)
                {
                    this.initialState = -1;
                    this.text = text;
                }

                //Returns true if differs from initial state
                public bool Check(string content, bool silent)
                {
                    int state = content.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0;

                    if (!silent)
                    {
                        Console.ForegroundColor = state != 0 ? ConsoleColor.Green : ConsoleColor.Red;
                        Console.Write('▓');
                    }

                    if (initialState < 0) initialState = state;
                    return initialState != state;
                }
            }

            public string name;
            public readonly string url;

            private List<SubTracker> subtrackers;

            public bool canNotify;

            public Tracker(string url)
            {
                this.name = "";
                this.url = url;
                this.subtrackers = new List<SubTracker>();
                this.canNotify = true;
            }

            public enum CheckResult
            {
                IS_DESIRED_STATE,
                IS_NOT_DESIRED_STATE,
                FAIL_TOO_MANY_REQUESTS,
                FAIL
            }

            public CheckResult Check(HttpClient web, bool silent)
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

                if (url.IndexOf("amazon", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    request.Headers.Add("Cookie", "" /*Ha, as if I'm going to put my cookies on github. Get your own!*/);
                }

                HttpResponseMessage response = web.SendAsync(request, HttpCompletionOption.ResponseContentRead).Result;

                if (response.IsSuccessStatusCode)
                {
                    string content = response.Content.ReadAsStringAsync().Result;

                    bool differsFromInitial = false;

                    foreach (var st in subtrackers)
                        if (st.Check(content, silent))
                            differsFromInitial = true;

                    return differsFromInitial ? CheckResult.IS_DESIRED_STATE : CheckResult.IS_NOT_DESIRED_STATE;
                }
                else if ((int)response.StatusCode == 429)
                {
                    return CheckResult.FAIL_TOO_MANY_REQUESTS;
                }

                return CheckResult.FAIL;
            }

            public void Add(string text)
            {
                subtrackers.Add(new SubTracker(text));
            }

            public void TrySetSTInitialState(bool initialState)
            {
                if (subtrackers.Count > 0)
                    subtrackers[subtrackers.Count - 1].initialState = initialState ? 1 : 0;
            }
        }

        private List<Tracker> trackers = new List<Tracker>();

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
                    tasks[i] = Task.Run(() => e.Check(client, true));
                }
            }

            for (int i = 0; i < trackers.Count; ++i)
            {
                Tracker e = trackers[i];
                Console.ForegroundColor = ConsoleColor.White;
                Console.Out.Write($"{(e.name.Length > 0 ? e.name : e.url)} ");

                Tracker.CheckResult checkResult = asyncMode ? tasks[i].Result : e.Check(client, false);

                if (!asyncMode)
                {
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

        //Attempts to set initial state of the latest tracker's latest subtracker
        public void TrySetTrackerSTState(bool state)
        {
            if (trackers.Count > 0)
                trackers[trackers.Count - 1].TrySetSTInitialState(state);
        }

        public int URLCount { get { return trackers.Count; } }
    }
}
