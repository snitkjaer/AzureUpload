﻿﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using System.IO;
using Microsoft.WindowsAzure.Storage.Blob; // Namespace for Blob storage types

//

namespace AzureUpload.Runner
{
    
    public class AzureBlobUpload
    {
        private readonly ILogger Logger;
        private ILoggerFactory LoggerFactory;

		public AzureBlobUpload(ILoggerFactory loggerFactory)
		{
            LoggerFactory = loggerFactory;
			Logger = loggerFactory
				.CreateLogger(typeof(AzureBlobUpload).FullName);
		}

        public CancellationToken cancellationToken;


		// Blob
		public string stringBlobUri = "";

        // Storage queue
        public string stringStorageQueueUri = "";


		public async Task<bool> UploadAsync(string filePath)
		{

			try
			{

				// Create the blob client. TODO: add cancellationToken
				CloudBlockBlob blobClient = new CloudBlockBlob(GenerateUriForFile(stringBlobUri, filePath));

				// Upload the file
				await blobClient.UploadFromFileAsync(filePath);

				Logger.LogInformation("Uploaded: " + filePath);
				return true;

			}
			catch (Exception ex)
			{

				Logger.LogError("Exception uploading file " + filePath, ex);
				return false;
			}
		}

		private Uri GenerateUriForFile(string url, string filePath)
		{
			// Get the file name (will be used to name the container i.e. files names must be unique or they will be overwritten)
			string fileName = Path.GetFileName(filePath);

			// Added the filename to the uri
			var str = stringBlobUri.Split('?');
			var newUri = str[0] + "/" + fileName + "?" + str[1];

			// Generate Uri object
			return new Uri(newUri);

		}


    }


}
