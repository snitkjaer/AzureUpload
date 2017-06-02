using System;
using PeterKottas.DotNetCore.WindowsService;

namespace AzureUpload.WindowsService
{
    class Program
    {
        public static void Main(string[] args)
		{
			ServiceRunner<UploadService>.Run(config =>
			{
				var name = config.GetDefaultName();
				config.Service(serviceConfig =>
				{
					serviceConfig.ServiceFactory((extraArguments) =>
					{
						return new UploadService();
					});

					serviceConfig.OnStart((service, extraParams) =>
					{
						Console.WriteLine("Service {0} started", name);
						service.Start();
					});

					serviceConfig.OnStop(service =>
					{
						Console.WriteLine("Service {0} stopped", name);
						service.Stop();
					});

					serviceConfig.OnError(e =>
					{
						Console.WriteLine("Service {0} exception occurred: {1}", name, e.Message);
					});
				});
			});
		}
    }
}


