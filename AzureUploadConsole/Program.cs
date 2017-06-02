﻿using System;
using System.Threading;
using AzureUpload.Runner;

namespace AzureUpload.ConsoleApp
{
    class Program
    {

		static void Main(string[] args)
        {

			
            ManualResetEvent stopEvent = new ManualResetEvent(false);

			Console.Clear();




            bool running = true;

            try
            {
                WatchFolder watchFolder = new WatchFolder();

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
            catch{}


        }


    }
}
