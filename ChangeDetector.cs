using System;
using System.Collections.Generic;
using System.Net.Http;

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
                public bool Check(string content)
                {
                    int state = content.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0;

                    Console.ForegroundColor = state != 0 ? ConsoleColor.Green : ConsoleColor.Red;
                    Console.Write('▓');

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

            public bool Check(HttpClient web)
            {
                HttpResponseMessage response = web.GetAsync(url).GetAwaiter().GetResult();

                if (response.IsSuccessStatusCode)
                {
                    string content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    bool differsFromInitial = false;

                    foreach (var st in subtrackers)
                        if (st.Check(content))
                            differsFromInitial = true;

                    return differsFromInitial;
                }
                else
                {
                    
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write((int)response.StatusCode == 429 ? "(TOO MANY REQUESTS)" : "(FAILED)");
                }

                return false;
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
        public void Check(HttpClient client, int msInterval, Notify notify)
        {
            foreach (Tracker e in trackers)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Out.Write($"{(e.name.Length > 0 ? e.name : e.url)} ");

                bool differsFromInitial = e.Check(client);

                Console.ForegroundColor = ConsoleColor.White;
                Console.Out.Write('|');

                Console.ForegroundColor = differsFromInitial ? ConsoleColor.Green : ConsoleColor.Red;
                Console.Out.Write("█ ");

                if (differsFromInitial)
                {
                    if (e.canNotify)
                    {
                        e.canNotify = false;
                        notify(e.name, e.url);
                    }
                }
                else
                {
                    e.canNotify = true;
                }

                System.Threading.Thread.Sleep(msInterval);
            }
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

        public bool IsEmpty { get { return trackers.Count == 0; } }
    }
}
