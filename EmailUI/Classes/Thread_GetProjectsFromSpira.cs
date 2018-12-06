using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using Inflectra.KronoDesk.Service.Email.Settings;
using Inflectra.KronoDesk.Service.Email.Settings.SpiraClient;
using System.Threading;

namespace Inflectra.KronoDesk.Service.Email.UI
{
	internal class Thread_Spira_GetProjects
	{
		#region Public Properties and Events
		/// <summary>Fired off once the import is finished (or cancelled.)</summary>
		public event EventHandler<SpiraFinishArgs> ProgressFinished;
		#endregion

		private const string CLASS_NAME = "Thread_Spira_GetProjects.";

		private string _serverURL;
		private string _userName;
		private string _userPass;

		/// <summary>Creates a new instance of the thread to get projects rom Spirateam.</summary>
		/// <param name="ServerURL">The URO of the Spirateam API.</param>
		/// <param name="UserName"></param>
		/// <param name="UserPassword"></param>
		public Thread_Spira_GetProjects(string ServerURL, string UserName, string UserPassword)
		{
			this._serverURL = ServerURL;
			this._userName = UserName;
			this._userPass = UserPassword;
		}

		/// <summary>Starts the thread.</summary>
		public void StartProcess()
		{
			try
			{
				//Connect to the server, get 
				Settings.SpiraClient.SoapServiceClient client = Settings.ClientFactory.CreateClient_Spira(new Uri(this._serverURL + "/" + ClientFactory.SPIRA_API));

				//Log in..
				if (client.Connection_Authenticate2(this._userName, this._userPass, Common.APP_NAME))
				{
					//Okay, try to get the list of project. 
					List<RemoteProject> projs = client.Project_Retrieve();

					//Got projects? Return 'em!
					if (this.ProgressFinished != null)
					{
						this.ProgressFinished(this, new SpiraFinishArgs(projs));
					}
				}
				else
				{
					if (this.ProgressFinished != null)
					{
						this.ProgressFinished(this, new SpiraFinishArgs(new Exception("Could not log in with username and password!")));
					}
				}
			}
			catch (Exception ex)
			{
				if (this.ProgressFinished != null)
				{
					this.ProgressFinished(this, new SpiraFinishArgs(new Exception("Could not connect to the server. Check your settings and try again.", ex)));
				}
			}
		}
	}

	/// <summary>Class for holding the projects or error.</summary>
	public class SpiraFinishArgs : EventArgs
	{
		public SpiraFinishArgs(Exception ex)
		{
			this.Error = ex;
		}

		public SpiraFinishArgs(List<RemoteProject> projects)
		{
			this.Projects = projects;
		}

		public Exception Error
		{ get; set; }

		public List<RemoteProject> Projects
		{ get; set; }
	}
}
