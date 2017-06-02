using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Microsoft.WindowsAzure.Storage.Blob; // Namespace for Blob storage types
using System.IO;


//

namespace AzureUploadConsole
{

	public class UploadFileToAzureBlob
	{
		private readonly ILogger Logger;
		private ILoggerFactory LoggerFactory;

        public string BlobUri { get; internal set; }
        public string FilePath { get; internal set; }

        public Task<bool> Upload;

		public UploadFileToAzureBlob(ILoggerFactory loggerFactory,string blobUri, string filePath)
		{
			BlobUri = blobUri;
            FilePath = filePath;
            Cleared = false;

			LoggerFactory = loggerFactory;
			Logger = loggerFactory
				.CreateLogger(typeof(Upload).FullName);

            // Start upload
            Upload = UploadAsync();
		}



		public bool Running 
        {
            get
            {
                if (Upload == null)
                    return false;

                switch(Upload.Status)
                {
                    case TaskStatus.WaitingForActivation:
                        return true;
                    default:
                        return false;
                }


            }
        }

        public bool Cleared { get; set; }


        public async Task<bool> UploadAsync()
		{

			try
			{

				// Create the blob client.
				CloudBlockBlob blobClient = new CloudBlockBlob(GenerateUriForFile(BlobUri, FilePath));

				// Upload the file
				await blobClient.UploadFromFileAsync(FilePath);

				Logger.LogInformation("Uploaded: " + FilePath);
				return true;

			}
			catch (Exception ex)
			{

				Logger.LogError("Exception uploading file " + FilePath, ex);
				return false;
			}
		}

		private Uri GenerateUriForFile(string url, string filePath)
		{
			// Get the file name (will be used to name the container i.e. files names must be unique or they will be overwritten)
			string fileName = Path.GetFileName(filePath);

			// Added the filename to the uri
			var str = BlobUri.Split('?');
			var newUri = str[0] + "/" + fileName + "?" + str[1];

			// Generate Uri object
			return new Uri(newUri);

		}
	}
}
