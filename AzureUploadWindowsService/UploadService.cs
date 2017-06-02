using System;
using PeterKottas.DotNetCore.WindowsService.Interfaces;
using AzureUpload.Runner;

namespace AzureUpload.WindowsService
{

	public class UploadService : IMicroService
	{
        WatchFolder watchFolder; 
    
		public void Start()
		{
			Console.WriteLine("Started service");
            watchFolder = new WatchFolder();
            watchFolder.Start();
		}

		public void Stop()
		{
            watchFolder.Stop();
			Console.WriteLine("Stopped service");
		}
	}

}
