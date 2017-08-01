using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

using AzureUpload.Runner;
using System.Threading;

namespace AzureUpload.Console
{
    class ConsoleApp
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        ManualResetEvent stopEvent = new ManualResetEvent(false);
        WatchFolder watchFolder;
        bool running = true;

        public void Run()
        {
            System.Console.Clear();

            //string version = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;


            try
            {
                string assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

                

                //System.Console.WriteLine("Starting version "+ version);
                watchFolder = new WatchFolder();
                watchFolder.DoneWatching += WatchFolder_DoneWatching;

                Log.Info("Starting version " + assemblyVersion);

                System.Console.CancelKeyPress += (s, e) =>
                {
                    Log.Info("Cancel pressed - stopping");
                    e.Cancel = true;
                    watchFolder.Stop();
                    running = false;
                    stopEvent.Set();
                };

                watchFolder.Start();

                while (running)
                {

                    System.Console.WriteLine("Press CTRL+C to stop:");

                    stopEvent.WaitOne();

                    Log.Info("Stopped");


                }
            }
            catch (Exception ex)
            {
                Log.Error("Exception", ex);
            }
        }

        private void WatchFolder_DoneWatching(object sender)
        {
            running = false;
            stopEvent.Set();
        }
    }
}
