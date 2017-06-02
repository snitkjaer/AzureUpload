using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace AzureUpload.Runner
{
    public class WatchFolder
    {

		// Configuration
		public static IConfigurationRoot Configuration { get; set; }

        public string WatchFolderPath = ".";
        public string FileExtensionFilter = "*.*";


		private ILogger Logger;
		private ILoggerFactory LoggerFactory;

        private TimeSpan CheckForNewFilesDelay = new TimeSpan(0, 0, 30);

        bool running = true;
        private Task taskWatchFolder;

        CancellationTokenSource cts;
        AzureEventHubSend SendEvent;
        AzureBlobUpload BlobUpload;
        private string Sender;

		public TimeSpan FileLastWriteTimeToUploadDelayDuration = new TimeSpan(0, 2, 0);
		public bool ValidateZipFile = true;
		public bool DeleteInvalidZipFiles = true;
		public bool DeleteFileOnSuccesfullUpload = true;

        ~WatchFolder()
        {
            running = false;
            cts.Cancel();
            LoggerFactory.Dispose();
        }


		public void Start()
		{
            Sender = System.Net.Dns.GetHostName();

			string appFolder = Directory.GetCurrentDirectory();

            try
            {
                if (!File.Exists(appFolder + "/appsettings.json"))
                {
                    Console.WriteLine("Configuration file was not found in " + appFolder + "/appsettings.json");
                    Console.WriteLine("aborting");
					throw new Exception("Missing configuration file");
                }
                // Init configuration
                var builder = new ConfigurationBuilder()
                    .SetBasePath(appFolder)
                    .AddJsonFile("appsettings.json");

                Configuration = builder.Build();
            }
            catch(Exception ex)
            {
                Console.WriteLine("Invalid configuration file exception" + ex.ToString());
                throw ex;
            }


			// Logging
			var loggingConfig = Configuration.GetSection("Logging");

			//Directory.CreateDirectory(appFolder + "/Logs");

			LoggerFactory = new LoggerFactory()
				.AddConsole()
				.AddFile(Configuration.GetSection("Logging"));
            
			Logger = LoggerFactory
			   .CreateLogger(typeof(WatchFolder));



            // Blob upload
            cts = new CancellationTokenSource();
            BlobUpload = new AzureBlobUpload(LoggerFactory);
            BlobUpload.cancellationToken = cts.Token;
			BlobUpload.stringBlobUri = Configuration.GetConnectionString("BlobContainerUri");

            // Event hub
            SendEvent = new AzureEventHubSend(LoggerFactory,
                                              Configuration.GetConnectionString("EventHubUri"),
                                              Configuration.GetSection("AppSettings")["EventHubEntityPath"]
                                              );

			// Config
			WatchFolderPath = Configuration.GetSection("AppSettings")["WatchFolderPath"];
            FileExtensionFilter = Configuration.GetSection("AppSettings")["FileExtensionFilter"];
			ValidateZipFile = Configuration.GetSection("AppSettings")["ValidateZip"].Get<bool>();
			DeleteInvalidZipFiles = Configuration.GetSection("AppSettings")["DeleteInvalidZipFiles"].Get<bool>();
			DeleteFileOnSuccesfullUpload = Configuration.GetSection("AppSettings")["DeleteFileOnSuccesfullUpload"].Get<bool>();
			FileLastWriteTimeToUploadDelayDuration = XmlConvert.ToTimeSpan(Configuration.GetSection("AppSettings")["FileLastWriteTimeToUploadDelayDuration"]);

            Logger.LogInformation("Watching folder: " + WatchFolderPath);

            // run
            taskWatchFolder = Task.Run(() => MonitorWatchFolderAsync(cts.Token));

		}



		public async Task MonitorWatchFolderAsync(CancellationToken token)
		{
			while (running)
			{
				try
				{
					await UploadAndRemoveFilesInFolder();

				}
				catch (Exception e)
				{
                    Logger.LogError("Exception",e);
				}
				await Task.Delay(CheckForNewFilesDelay, token);
			}
		}


		public void Stop()
		{
            running = false;
            cts.Cancel();
			try
			{
				taskWatchFolder.Wait();
			}
			catch (Exception e)
			{
				//Logger.LogError("Exception stopping folder monitor", e);
			}


		}





		public async Task UploadAndRemoveFilesInFolder()
		{

			try
			{

				List<string> uploadFileList = new List<string>();

				// Get files that match the extension filter
				IEnumerable<string> filePaths = Directory.GetFiles(WatchFolderPath, "*.*", SearchOption.TopDirectoryOnly)
										 .Where(s => FileExtensionFilter.Contains(Path.GetExtension(s)));




				foreach (var filePath in filePaths)
				{
					// Make sure the file has not been written to for some time
					DateTime lastWriteTime = File.GetLastWriteTime(filePath);

					// Make sure the file has not been written to for some time
					if (lastWriteTime.Add(FileLastWriteTimeToUploadDelayDuration) < DateTimeOffset.UtcNow)
					{
						// If it's .zip file and it's invalid -> delete it
						if (ValidateZipFile && FileExtensionFilter.ToLower() == "*.zip" && !filePath.ValidateZip())
						{
							// Delete invalid zip file
							if (DeleteInvalidZipFiles)
								DeleteFile(filePath);

							// Skip file
							continue;
						}

						// Add to upload file list
						uploadFileList.Add(filePath);


					}
				}

				// upload valid files
				await UploadAndDeleteFilesInQueue(uploadFileList);


			}
			catch (Exception ex)
			{

				Logger.LogError("Exception ", ex);
			}
		}

		public async Task UploadAndDeleteFilesInQueue(List<string> fileList)
		{
			try
			{
				foreach (string filePath in fileList)
				{
                    
                    if (await BlobUpload.UploadAsync(filePath))
                    {
						// Add to event hub
						string fileName = Path.GetFileName(filePath);
                        if (await SendEvent.SendMessage(fileName, Sender))
                        {
                            // Success in uplaod and message send
                            if (DeleteFileOnSuccesfullUpload)
                                DeleteFile(filePath);
                        }

                    }					
				}

			}
			catch (Exception ex)
			{

				Logger.LogError("Exception ", ex);

			}
		}


		private void DeleteFile(string filePath)
		{
			try
			{
				File.Delete(filePath);
				Logger.LogInformation("Deleted " + filePath);
			}
			catch (Exception ex)
			{
				Logger.LogError("DeleteFile", ex);
			}
		}
    }
}
