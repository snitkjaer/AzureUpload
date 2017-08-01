using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

using System.Threading;
using AzureUpload.Runner;

namespace AzureUpload.WindowsService
{
    public partial class AzureUploadWindowsService : ServiceBase
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        WatchFolder watchFolder;

        public AzureUploadWindowsService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            
            try
            {
                string assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                Log.Info("Starting version " + assemblyVersion);

                watchFolder = new WatchFolder();
                watchFolder.KeepRunning = true;
                watchFolder.Start();

            }
            catch (Exception ex)
            {
                Log.Error("Exception starting service ", ex);
            }
        }

        protected override void OnStop()
        {
            try
            {
                Log.Info("Stopping");
                if(watchFolder != null)
                    watchFolder.Stop();
            }
            catch(Exception ex)
            {
                Log.Error("Exception stopping service ", ex);
            }
        }


    }
}
