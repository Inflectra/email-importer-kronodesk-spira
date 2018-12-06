using System.IO;
using Inflectra.KronoDesk.Service.Email.Settings;
using System.Xml.Serialization;
using System;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using Inflectra.POP3.Mime;
using Inflectra.POP3;
using System.Security.AccessControl;

namespace Inflectra.KronoDesk.Service.Email.Service
{
	/// <summary>The class that does the main workload.</summary>
	public partial class PollThread
	{
		private const string CLASS = "Inflectra.KronoDesk.Service.Email.Service.PollThread::";
		private SettingsStorage_100 _settings;
		private FileSystemWatcher _configWatcher;
		private Logger _eventLog;
		private Timer _timer;
		//private const int START_DELAY = 5;
		private const int MILI = 1000 * 60; //Add 60 for seconds. Config file stores value in seconds.

		/// <summary>Constructor. Creates the thread object, but does not start it.</summary>
		public PollThread()
		{
			const string METHOD = CLASS + ".ctor()";
			try
			{
				//Create a new log..
				_eventLog = new Logger(Common.APP_NAME);

				//See if our settings file exists, if not create it so that the service can start
				string inflectraDir = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Inflectra");
				string settingsDir = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Common.APP_MANUF + "\\" + Common.APP_DIRECTORY);
				if (!Directory.Exists(settingsDir))
				{
					Directory.CreateDirectory(settingsDir);

					//Update security on the two folders
					DirectorySecurity dirSecurity = Directory.GetAccessControl(inflectraDir);
					dirSecurity.AddAccessRule(new FileSystemAccessRule("Users",
																 FileSystemRights.FullControl,
																 AccessControlType.Allow));
					Directory.SetAccessControl(inflectraDir, dirSecurity);
					dirSecurity = Directory.GetAccessControl(settingsDir);
					dirSecurity.AddAccessRule(new FileSystemAccessRule("Users",
																 FileSystemRights.FullControl,
																 AccessControlType.Allow));
					Directory.SetAccessControl(settingsDir, dirSecurity);
				}
				string fileName = Path.Combine(settingsDir, Common.SETTINGS_FILE);
				if (!File.Exists(fileName))
				{
					using (FileStream fs = File.Open(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
					{
						SettingsStorage_100 settings = new SettingsStorage_100();
						XmlSerializer ser = new XmlSerializer(settings.GetType());
						ser.Serialize(fs, settings);
						fs.Close();
					}

					//Need to allow all users to modify this file (since the main UI will run as a different user)
					FileSecurity fileSecurity = File.GetAccessControl(fileName);
					fileSecurity.AddAccessRule(new FileSystemAccessRule("Users",
																 FileSystemRights.FullControl,
																 AccessControlType.Allow));
					File.SetAccessControl(fileName, fileSecurity);
				}

				//First create an event on our config file.
				_configWatcher = new FileSystemWatcher();
				_configWatcher.Path = settingsDir;
				_configWatcher.Filter = Common.SETTINGS_FILE;
				_configWatcher.IncludeSubdirectories = false;
				_configWatcher.NotifyFilter = NotifyFilters.Attributes | NotifyFilters.LastWrite | NotifyFilters.Size;
				_configWatcher.Changed += new FileSystemEventHandler(configFile_Changed);

				//Read current configuration, first, then start watcher.
				readSettingsFile();
				_configWatcher.EnableRaisingEvents = true;
			}
			catch (Exception ex)
			{
				//An error occured. Try logging it first, and then rethrow it.
				try
				{
					_eventLog.WriteMessage(METHOD, ex);
				}
				catch { }

				//Rethrow exception.
				throw;
			}
		}

		/// <summary>Starts the actual polling.</summary>
		public void Start()
		{
			//DEBUG: Sleep 10 seconds to connect to process.
			Thread.Sleep(10000);

			//Now for our loop. Set the Process Run first..
			_timer = new Timer(TimerEvent, this, 0, _settings.PollInterval * MILI);
		}

		/// <summary>Set false to stop the processing.</summary>
		public bool ProcessRun
		{ get; set; }

		/// <summary>Hit when the timer expires. This does the hard work!</summary>
		/// <param name="state">This object.</param>
		private void TimerEvent(object state)
		{
			const string METHOD = CLASS + "TimerEvent()";
			_eventLog.EntryLog(METHOD);

			//Pause the timer, set thread name.
			_eventLog.WriteTrace(METHOD, "Pausing timer.");
			_timer.Change(Timeout.Infinite, Timeout.Infinite);
			Thread.CurrentThread.Name = Common.APP_NAME + ": Email Polling Thread";

			//Now loop through each email account.
			foreach (AccountDetails account in _settings.Email_Accounts)
			{
				try
				{
					_eventLog.WriteTrace(METHOD, "Running on account: '" + account.AccountEmail + "' (" + account.AccountID.ToString() + ")");

					//Get the appl's server definition to make sure it exists.
					ApplicationSystem serverDef = _settings.Application_Servers.Where(ap => ap.ServerID == account.ApplicationServerID).SingleOrDefault();

					if (account.ApplicationServerID > 0 && //There is a App Server selected.
						serverDef != null && //And the AppServer exists.
						(account.ProductOrProjectId.HasValue && account.ProductOrProjectId.Value > 0))  //And there's a defined default Product/Project.
					{
						//Get the POP3 client, first.
						//TODO: This should be moved to inside each App Process.
						Pop3Client clientPOP3 = popConnect(account);

						if (serverDef.ServerType == ApplicationSystem.ApplServerTypeEnum.Spira)
						{
							processSpiraAccount(clientPOP3, account, serverDef);
						}
						else if (serverDef.ServerType == ApplicationSystem.ApplServerTypeEnum.Krono)
						{
							processKronoAccount(clientPOP3, account, serverDef);
						}
					}
					else
					{
						string msg = METHOD + Environment.NewLine +
							"Not running on account:" + Environment.NewLine +
							"Name: " + account.AccountEmail + Environment.NewLine +
							"ID: " + account.AccountID.ToString() + Environment.NewLine +
							"Server: " + account.ServerNameOrIP + Environment.NewLine +
							"Username: " + account.UserLogin + Environment.NewLine + Environment.NewLine +
							"No application server or default product or project is defined. Configuration needs to be updated.";

						_eventLog.WriteMessage(msg, EventLogEntryType.Error);
					}
				}
				catch (Exception ex)
				{
					//Log the error, so we know this one failed.
					_eventLog.WriteMessage(METHOD, ex);
				}
			}
			//Resume timer.
			_timer.Change(_settings.PollInterval * MILI, _settings.PollInterval * MILI);
		}

		/// <summary>Creates and returns a WSDL client for the given application system. Will try to connect and log on and select project if applicable. If any fails, an exception will be thrown.</summary>
		/// <param name="applicationSystem">The application system definition.</param>
		/// <returns>A client, as a dynamic type.</returns>
		private dynamic CreateApplicationClient(ApplicationSystem applicationSystem, AccountDetails emailSystem)
		{
            const string METHOD = CLASS + "CreateApplicationClient()";
			_eventLog.EntryLog(METHOD);

			//Create our application server.
			dynamic clientApp;

			if (applicationSystem.ServerType == ApplicationSystem.ApplServerTypeEnum.Spira)
			{
				//Create the client.
				clientApp = ClientFactory.CreateClient_Spira(new Uri(applicationSystem.ServerAPIUrl));

				//Connect.
				_eventLog.WriteTrace(METHOD, "Logging into Spira Client.");
				Settings.SpiraClient.SoapServiceClient spiraClient = (Settings.SpiraClient.SoapServiceClient)clientApp;
				if (spiraClient.Connection_Authenticate2(applicationSystem.UserID, applicationSystem.UserPassword, Common.APP_NAME))
				{
					_eventLog.WriteTrace(METHOD, "Selecting project in Spira Client.");
					if (emailSystem.ProductOrProjectId.HasValue && spiraClient.Connection_ConnectToProject(emailSystem.ProductOrProjectId.Value))
					{
						//We're successful.
					}
					else
					{
						throw new Exception("Could not log into project #" + emailSystem.ProductOrProjectId.Value.ToSafeString());
					}
				}
				else
				{
					throw new Exception("Could not log into server.");
				}
			}
			else
			{
				//Create the client.
				clientApp = ClientFactory.CreateClient_Krono(new Uri(applicationSystem.ServerAPIUrl));

				//Connect.
				_eventLog.WriteTrace(METHOD, "Logging into Krono Client.");
				Settings.KronoClient.SoapServiceClient kronoClient = (Settings.KronoClient.SoapServiceClient)clientApp;
				if (kronoClient.Connection_Authenticate(applicationSystem.UserID, applicationSystem.UserPassword, Common.APP_NAME, true))
				{ }
				else
				{
					throw new Exception("Could not log into server.");
				}
			}
			return clientApp;
		}
	}
}
