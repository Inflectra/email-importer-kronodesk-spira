using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml.Serialization;
using Inflectra.KronoDesk.Service.Email.Settings;

namespace Inflectra.KronoDesk.Service.Email.Service
{
	/// <summary>Sections of the class that contains file functions.</summary>
	public partial class PollThread
	{
		/// <summary>Reads the given file of message UIDs into memory.</summary>
		/// <param name="accountId">The ID to get the UIDs for.</param>
		/// <returns>List of known UIDs</returns>
		private List<string> readMessageIDsForAccount(int accountId)
		{
			const string METHOD = CLASS + "readAccountUIDs()";
			this._eventLog.EntryLog(METHOD);

			List<string> retList = new List<string>();

			try
			{
				//First see if the file exists..
				string fileName = "acct_" + accountId.ToString() + ".uid";
				string path = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + "\\" + Common.APP_MANUF + "\\" + Common.APP_DIRECTORY + "\\";

				this._eventLog.WriteTrace(METHOD, "Reading account file: " + Environment.NewLine + path + fileName);

				if (File.Exists(path + fileName))
				{
					//Read our file here.
					retList = new List<string>(File.ReadAllLines(path + fileName));
				}
				else
				{
					this._eventLog.WriteTrace(METHOD, "File did not exist. Creating file.");

					//Create the file, and return an empty list.
					if (!Directory.Exists(path))
						Directory.CreateDirectory(path);

					//Create our file
					File.WriteAllBytes(path + fileName, new byte[] { });
				}
			}
			catch (Exception ex)
			{
				this._eventLog.WriteMessage(METHOD, ex, "Loading account message IDs.");
				retList = new List<string>();
			}

			this._eventLog.ExitLog(METHOD);
			return retList;
		}

		/// <summary>Saves the given message UIDs to the account file.</summary>
		/// <param name="accountId">The account ID.</param>
		/// <param name="messageUIDs">The list of message IDs.</param>
		private void saveMessageIDsForAccount(int accountId, List<string> messageUIDs)
		{
			const string METHOD = CLASS + "saveAccountIDs()";
			this._eventLog.EntryLog(METHOD);

			try
			{
				//First see if the file exists..
				string fileName = "acct_" + accountId.ToString() + ".uid";
				string path = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + "\\" + Common.APP_MANUF + "\\" + Common.APP_DIRECTORY + "\\";
				this._eventLog.WriteTrace(METHOD, "Saving account file: " + Environment.NewLine + path + fileName);

				if (!Directory.Exists(path))
				{
					this._eventLog.WriteTrace(METHOD, "Directory (and file) does not exist. Creating directory.");
					Directory.CreateDirectory(path);
				}

				//Write to the file.
				File.WriteAllLines(path + fileName, messageUIDs);
			}
			catch (Exception ex)
			{
				this._eventLog.WriteMessage(METHOD, ex, "Saving UIDs to file.");
			}
		}

		/// <summary>Reads the configuration file.</summary>
		private void readSettingsFile()
		{
			const string METHOD = CLASS + "readSettingsFile()";
			this._eventLog.EntryLog(METHOD);

			try
			{
				//Just in case it fires an event:
				this._configWatcher.EnableRaisingEvents = false;

				string fileName = this._configWatcher.Path + "\\" + this._configWatcher.Filter;
				this._eventLog.WriteTrace(METHOD, "Using config file:" + Environment.NewLine + fileName);
				if (File.Exists(fileName))
				{
					//Load the XML.
					XmlSerializer xmlSer = new XmlSerializer(typeof(SettingsStorage_100));
					using (FileStream fileStream = new FileStream(fileName, FileMode.Open))
					{
						this._settings = (SettingsStorage_100)xmlSer.Deserialize(fileStream);
					}
				}
				else
				{
					//We need to throw an exception.
					throw new Exception("Configuration file missing. You must run the config application first!");
				}

				//Re-Enable events.
				this._configWatcher.EnableRaisingEvents = true;
			}
			catch (Exception ex)
			{
				this._eventLog.WriteMessage(METHOD, ex);
				throw;
			}

			this._eventLog.ExitLog(METHOD);
		}

		/// <summary>Hit when the config file changes.</summary>
		/// <param name="sender">FileSystemWatcher</param>
		/// <param name="e">FileSystemEventArgs</param>
		private void configFile_Changed(object sender, FileSystemEventArgs e)
		{
			const string METHOD = CLASS + "configFile_Changed()";
			this._eventLog.EntryLog(METHOD);

			//Pause the timer.
			this._eventLog.WriteTrace(METHOD, "Pausing timer.");
			this._timer.Change(Timeout.Infinite, Timeout.Infinite);

			int tries = 0;
			while (tries < 5)
			{
				try
				{
					//Reread the config file.
					this.readSettingsFile();
					tries = 10; //We got this far, we're good.
				}
				catch
				{
					//If an error was thrown, advance counter, wait, and try again.
					Thread.Sleep(1000);
					tries++;
				}
			}

			//Resume the timer, do not fire timer off immediately.
			this._eventLog.WriteTrace(METHOD, "Resuming timer.");
			this._timer.Change(this._settings.PollInterval * MILI, this._settings.PollInterval * MILI);
		}
	}
}
