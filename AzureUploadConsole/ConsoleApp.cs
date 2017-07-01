using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

using AzureUpload.Runner;
using System.Threading;

namespace AzureUpload.ConsoleApp
{
    class ConsoleApp
    {
        ManualResetEvent stopEvent = new ManualResetEvent(false);
        WatchFolder watchFolder;
        bool running = true;

        public void run()
        {
            Console.Clear();

            string version = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;


            try
            {
                Console.WriteLine("Starting version "+ version);
                watchFolder = new WatchFolder();
                watchFolder.DoneWatching += WatchFolder_DoneWatching;

                System.Console.CancelKeyPress += (s, e) =>
                {
                    Console.WriteLine("Stopping");
                    e.Cancel = true;
                    watchFolder.Stop();
                    running = false;
                    stopEvent.Set();
                };

                watchFolder.Start();

                while (running)
                {

                    Console.WriteLine("Press CTRL+C to stop:");

                    stopEvent.WaitOne();

                    Console.WriteLine("Stopped");


                }
            }
            catch { }
        }

        private void WatchFolder_DoneWatching(object sender)
        {
            running = false;
            stopEvent.Set();
        }
    }
}
