using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.EventHubs;

namespace AzureUpload.Runner
{
    public class AzureEventHubSend
    {
		private static EventHubClient eventHubClient;

		private readonly ILogger Logger;
		private ILoggerFactory LoggerFactory;

		public AzureEventHubSend(ILoggerFactory loggerFactory, string EhConnectionString,string EhEntityPath)
		{
			LoggerFactory = loggerFactory;
			Logger = loggerFactory
				.CreateLogger(typeof(AzureBlobUpload).FullName);

			
			try
			{
				var connectionStringBuilder = new EventHubsConnectionStringBuilder(EhConnectionString)
				{
					EntityPath = EhEntityPath
				};
				eventHubClient = EventHubClient.CreateFromConnectionString(connectionStringBuilder.ToString());
			}
			catch (Exception ex)
			{
				Logger.LogError("Exception connecting to event hub", ex);
                throw ex;
			}
		}

        ~AzureEventHubSend()
        {
            eventHubClient.Close();
        }

        public async Task<bool> SendMessage(string message, string sender)
        {
            if (eventHubClient == null)
                return false;

            try
            {
				// Create a new EventData object by encoding a string as a byte array
				var data = new EventData(Encoding.UTF8.GetBytes(message));
				// Set user properties if needed
				data.Properties.Add("Sender", sender);
				// Send single message async
				await eventHubClient.SendAsync(data);
                return true;
            }
            catch(Exception ex)
            {
                Logger.LogError("Exception sending message" + message + ex.ToString());
                return false;
            }

        }



    }
}
