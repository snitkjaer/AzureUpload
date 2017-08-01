using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

using log4net;

namespace AzureUpload.Runner
{
    public class WatchFolder
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Configuration
        public static IConfigurationRoot Configuration { get; set; }
        public string WatchFolderPath = ".";
        public string FileExtensionFilter = "*.*";


        private TimeSpan CheckForNewFilesDelay = new TimeSpan(0, 0, 30);

        public bool KeepRunning = true;
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
            KeepRunning = false;
            if(cts != null)
                cts.Cancel();
            if (log != null)
                LogManager.Shutdown();
        }



        #region DoneWatching Event
        // Delegate
        public delegate void DoneWatchingEventHandler(object sender);

        // The event
        public event DoneWatchingEventHandler DoneWatching;

        // The method which fires the Event
        protected void OnDoneWatching(object sender)
        {
            // Check if there are any Subscribers
            // Call the Event
            DoneWatching?.Invoke(this);
        }
        #endregion

        public void Start()
		{
            Sender = System.Net.Dns.GetHostName();

            string appFolder = AppDomain.CurrentDomain.BaseDirectory;

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
			Directory.CreateDirectory(appFolder + "/Logs");



            // Blob upload
            cts = new CancellationTokenSource();
            BlobUpload = new AzureBlobUpload();
            BlobUpload.cancellationToken = cts.Token;
			BlobUpload.stringBlobUri = Configuration.GetConnectionString("BlobContainerUri");
            

            // Event hub
            SendEvent = new AzureEventHubSend(Configuration.GetConnectionString("EventHubUri"));
                                              

			// Config
			WatchFolderPath = Configuration.GetSection("AppSettings")["WatchFolderPath"];
            KeepRunning = Configuration.GetSection("AppSettings")["KeepRunning"].Get<bool>();
            FileExtensionFilter = Configuration.GetSection("AppSettings")["FileExtensionFilter"];
			ValidateZipFile = Configuration.GetSection("AppSettings")["ValidateZip"].Get<bool>();
			DeleteInvalidZipFiles = Configuration.GetSection("AppSettings")["DeleteInvalidZipFiles"].Get<bool>();
			DeleteFileOnSuccesfullUpload = Configuration.GetSection("AppSettings")["DeleteFileOnSuccesfullUpload"].Get<bool>();
			FileLastWriteTimeToUploadDelayDuration = XmlConvert.ToTimeSpan(Configuration.GetSection("AppSettings")["FileLastWriteTimeToUploadDelayDuration"]);

            if(KeepRunning)
                log.Info("Watching folder: " + WatchFolderPath);
            else
                log.Info("Uploading files in folder: " + WatchFolderPath);

            // run
            taskWatchFolder = Task.Run(() => MonitorWatchFolderAsync(cts.Token));

		}



		public async Task MonitorWatchFolderAsync(CancellationToken token)
		{
            do
            {
                try
                {
                    await UploadAndRemoveFilesInFolder();

                }
                catch (Exception e)
                {
                    log.Error("Exception", e);
                }
                if (KeepRunning)
                    await Task.Delay(CheckForNewFilesDelay, token);
                else
                    OnDoneWatching(this);
            } 
            while (KeepRunning);
		}


		public void Stop()
		{
            KeepRunning = false;
            cts.Cancel();
			try
			{
				taskWatchFolder.Wait();
			}
			catch
			{
				//Logger.LogError("Exception stopping folder monitor", e);
			}


		}





		public async Task UploadAndRemoveFilesInFolder()
		{

			try
			{

				List<string> uploadFileList = new List<string>();

                if (!Directory.Exists(WatchFolderPath))
                {
                    log.Error("Watch folder does not exist :" + WatchFolderPath);
                    return;
                }
                

                // Get files that match the extension filter
                IEnumerable<string> filePaths = Directory.GetFiles(WatchFolderPath, "*.*", SearchOption.TopDirectoryOnly)
										 .Where(s => FileExtensionFilter.Contains(Path.GetExtension(s)));


                if(filePaths.Count() < 1)
                {
                    log.Debug("No files found");
                }


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

                log.Error("Exception ", ex);
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

                log.Error("Exception ", ex);

			}
		}


		private void DeleteFile(string filePath)
		{
			try
			{
				File.Delete(filePath);
                log.Info("Deleted " + filePath);
			}
			catch (Exception ex)
			{
                log.Error("DeleteFile", ex);
			}
		}

        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }
    }
}
