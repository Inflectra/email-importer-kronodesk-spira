using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Inflectra.KronoDesk.Service.Email.Settings;
using System.Security;
using Microsoft.Win32;
using System.Threading;

namespace Inflectra.KronoDesk.Service.Email.UI.Classes
{
	internal class Logger
	{
		private EventLog _eventLog;

		public Logger()
		{
			//Initialize the eventLog..
			this.createSource(); //Create source, if needed.
			this._eventLog = new EventLog();
			this._eventLog.BeginInit();
			this._eventLog.Source = Common.APP_NAME;
			this._eventLog.Log = "Application";
			this._eventLog.EndInit();

			//HACK:
			this.TraceEnabled = true;
		}

		#region Public Functions
		/// <summary>Writes the message to the event log.</summary>
		/// <param name="message">The message to write to the application log.</param>
		/// <param name="type">The type of the message being recorded.</param>
		/// <param name="eventId">The event ID, if any.</param>
		public void WriteMessage(string message, EventLogEntryType type = EventLogEntryType.Information, int eventId = 0)
		{
			if (type == EventLogEntryType.SuccessAudit && this.TraceEnabled)
				type = EventLogEntryType.Information;

			if (type != EventLogEntryType.SuccessAudit && type != EventLogEntryType.FailureAudit)
				this._eventLog.WriteEntry(message, type, eventId);

			//Write to the output panel..
			Debug.WriteLine(message);
		}

		/// <summary>Writes the exception to the event log.</summary>
		/// <param name="ex">The exception to record.</param>
		/// <param name="method">The method/class to record.</param>
		/// <param name="message">Any aditional message.</param>
		/// <param name="eventId">The event ID, if one.</param>
		public void WriteMessage(string method, Exception ex, string message = null, int eventId = 0)
		{
			string strLog = method + ":" + Environment.NewLine
				+ ((string.IsNullOrWhiteSpace(message)) ? "" : message + Environment.NewLine)
				+ this.getFromException(ex);

			//It's an exception, it's always an error.
			this.WriteMessage(strLog, EventLogEntryType.Error, eventId);
		}

		/// <summary>Records an entry to method, if tracing is on.</summary>
		/// <param name="method">The method name.</param>
		public void EntryLog(string method)
		{
			this.WriteMessage("-=>  Entering " + method, EventLogEntryType.SuccessAudit);
		}

		/// <summary>Records an exit from method, if tracing is on.</summary>
		/// <param name="method">The method name.</param>
		public void ExitLog(string method)
		{
			this.WriteMessage("<=-  Exiting " + method, EventLogEntryType.SuccessAudit);
		}

		/// <summary>Writes a trace message.</summary>
		/// <param name="method">The method making the call.</param>
		/// <param name="message">The message to record.</param>
		/// <param name="eventId">The event ID, if any.</param>
		public void WriteTrace(string method, string message, int eventId = 0)
		{
#if DEBUG
			if (this.TraceEnabled)
				this.WriteMessage(method + Environment.NewLine + message, EventLogEntryType.Information, eventId);
#endif
		}
		#endregion

		#region Private Functions
		/// <summary>Gets a nice log string from the given exception.</summary>
		/// <param name="ex">The exception to translate!</param>
		/// <returns>A formatted string.</returns>
		private string getFromException(Exception ex)
		{
			string stackTrace = ex.StackTrace;
			string message = ex.Message + " [" + ex.GetType().ToString() + "]";

			while (ex.InnerException != null)
			{
				message += Environment.NewLine + ex.InnerException.Message + " [" + ex.InnerException.GetType().ToString() + "]";
				ex = ex.InnerException;
			}

			//Now add the stacktrace:
			message += Environment.NewLine + Environment.NewLine + stackTrace;

			return message;
		}

		/// <summary>Creates the source manually.</summary>
		private void createSource()
		{
			Thread.Sleep(15000);

			string eventLogName = "Application";

			//Check whether the Event Source exists. It is possible that this may
			//  raise a security exception if the current process account doesn't
			//  have permissions for all sub-keys under
			//  HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\EventLog

			// Check whether registry key for source exists
			string keyName = @"SYSTEM\CurrentControlSet\Services\EventLog\" + eventLogName;
			RegistryKey rkEventSource = Registry.LocalMachine.OpenSubKey(keyName + @"\" + Common.APP_NAME);

			// Check whether key exists
			if (rkEventSource == null)
			{
				/// Key does not exist. Create key which represents source
				Registry.LocalMachine.CreateSubKey(keyName + @"\" + Common.APP_NAME);
			}

			/// Now validate that the .NET Event Message File, EventMessageFile.dll (which correctly
			/// formats the content in a Log Message) is set for the event source
			object eventMessageFile = rkEventSource.GetValue("EventMessageFile");

			/// If the event Source Message File is not set, then set the Event Source message file.
			if (eventMessageFile == null)
			{
				/// Source Event File Doesn't exist - determine .NET framework location,
				/// for Event Messages file.
				RegistryKey dotNetFrameworkSettings = Registry.LocalMachine.OpenSubKey(
					@"SOFTWARE\Microsoft\.NetFramework\");

				if (dotNetFrameworkSettings != null)
				{

					object dotNetInstallRoot = dotNetFrameworkSettings.GetValue(
						"InstallRoot",
						null,
						RegistryValueOptions.None);

					if (dotNetInstallRoot != null)
					{
						string eventMessageFileLocation = dotNetInstallRoot.ToString() + "v" + System.Environment.Version.Major.ToString() + "." + System.Environment.Version.Minor.ToString() + "." + System.Environment.Version.Build.ToString() + @"\EventLogMessages.dll";

						// Validate File exists
						if (System.IO.File.Exists(eventMessageFileLocation))
						{
							// The Event Message File exists in the anticipated location on the
							// machine. Set this value for the new Event Source

							// Re-open the key as writable
							rkEventSource = Registry.LocalMachine.OpenSubKey(keyName + @"\" + Common.APP_NAME, true);

							// Set the "EventMessageFile" property
							rkEventSource.SetValue("EventMessageFile", eventMessageFileLocation, RegistryValueKind.String);
						}
					}
				}

				dotNetFrameworkSettings.Close();
			}

			rkEventSource.Close();
		}
		#endregion

		#region Properties
		/// <summary>Enable TraceLogging or not.</summary>
		public bool TraceEnabled
		{ get; set; }
		#endregion

	}
}