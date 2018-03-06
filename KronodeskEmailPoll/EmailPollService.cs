using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace Inflectra.KronoDesk.Service.Email.Service
{
	/// <summary>This class is fired off when the service starts. It's the main thread, starting the polling thread in the background.</summary>
	public class EmailPollService : ServiceBase
	{
		//The thread that's executing.
		Thread _bkgThread;

		/// <summary>Initializes class.</summary>
		public EmailPollService()
		{
			this.InitializeComponent();
		}

		/// <summary>Initializer.</summary>
		private void InitializeComponent()
		{
			this.CanPauseAndContinue = true;
			this.ServiceName = "InflectraEmailIntegrationService";
		}

		/// <summary>Hit when the service is going to start.</summary>
		/// <param name="args">Arguments for polling.</param>
		protected override void OnStart(string[] args)
		{
			//Create a thread and start it.
			PollThread poll = new PollThread();
			this._bkgThread = new Thread(poll.Start);
			this._bkgThread.IsBackground = true;
            this._bkgThread.Name = "Inflectra Email Integration Service";
			this._bkgThread.Priority = ThreadPriority.BelowNormal;

			//Start thread.
			this._bkgThread.Start();
		}

		/// <summary>Hit when users want to stop the service.</summary>
		protected override void OnStop()
		{
			try
			{
				this._bkgThread.Join(5000);
				this._bkgThread.Abort();
				this._bkgThread = null;
			}
			catch { }
		}
	}
}
