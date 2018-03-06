using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Inflectra.KronoDesk.Service.Email.Settings;

namespace Inflectra.KronoDesk.Service.Email.UI
{
	/// <summary>
	/// Interaction logic for Details_ApplicationServer.xaml
	/// </summary>
	public partial class Details_ApplicationServer : Window
	{
		#region Properties
		/// <summary>The application system that the user wants to view or edit.</summary>
		public ApplicationSystem AppSystem
		{
			get
			{
				return this._appSystem;
			}
			set
			{
				this._appSystem = value;
				//this.loadData();
			}
		}
		private ApplicationSystem _appSystem;
		#endregion //Properties

		/// <summary>Flag to keep whether the window was first shown or not.</summary>
		private bool _loaded = false;
		/// <summary>Flag to montior whether background testing thread is running or not.</summary>
		private bool isBackgroundRunning = false;

		#region Constructors
		/// <summary>Creates new instance of the window.</summary>
		public Details_ApplicationServer()
		{
			this.InitializeComponent();
		}

		/// <summary>Creates a new instance of the window.</summary>
		/// <param name="system">The Application System the user wants to view or edit.</param>
		public Details_ApplicationServer(ApplicationSystem system)
		{
			this.InitializeComponent();
			this.AppSystem = system;
		}
		#endregion //Constructors

		#region Events
		/// <summary>Hit when the window is first displayed, copies information from the Application Server into the controls.</summary>
		/// <param name="e">EventArgs</param>
		protected override void OnContentRendered(EventArgs e)
		{
			base.OnContentRendered(e);

			//Load up ApplicationSystem data, if needed.
			if (!_loaded)
			{
				this.loadData();
				this._loaded = true;
			}

			this.SizeToContent = System.Windows.SizeToContent.Manual;
		}

		/// <summary>Hit when the user closes the dialog without saving.</summary>
		/// <param name="sender">btnCancel</param>
		/// <param name="e">RoutedEventArgs</param>
		private void btnCancel_Click(object sender, RoutedEventArgs e)
		{
			//We don't do anything. Close the window.
			this.DialogResult = false;
			this.Close();
		}

		/// <summary>Hit when the user wants to save and close the window.</summary>
		/// <param name="sender">btnSave</param>
		/// <param name="e">RoutedEventArgs</param>
		private void btnSave_Click(object sender, RoutedEventArgs e)
		{
			if (this.saveData())
			{
				this.DialogResult = true;
				this.Close();
			}
		}

		/// <summary>Hit when the user changes the server type.</summary>
		/// <param name="sender">RadioButton</param>
		/// <param name="e">RoutedEventArgs</param>
		private void rdoServerType_Checked(object sender, RoutedEventArgs e)
		{
			try
			{
				if (this.lblLoginSuffix != null)
					this.lblLoginSuffix.Content = ((this.rdoKrono.IsChecked.Value) ? Common.LOGINSUF_KRONO : Common.LOGINSUF_SPIRA);
			}
			catch { }
		}

		/// <summary>Hit when the user wants to test their connection to the server.</summary>
		/// <param name="sender">btnTest</param>
		/// <param name="e">RoutedEventArgs</param>
		private void btnTest_Click(object sender, RoutedEventArgs e)
		{
			//Check that form is entered as necessary. Create background data class.
			this.lblTestResult.Content = null;
			if (this.checkForm())
			{
				//Create our background values class.
				bool isSpira = (this.rdoSpira.IsChecked.HasValue && this.rdoSpira.IsChecked.Value);
				Uri serverUri = new Uri(this.cmbHttps.Text + this.txtServerURL.Text.Trim() + "/" + ((isSpira) ? ClientFactory.SPIRA_API : ClientFactory.KRONO_API));
				btnTest_DoWorkArgs workArgs = new btnTest_DoWorkArgs(isSpira, serverUri, this.txtUserName.Text.Trim(), this.txtUserPass.Password);

				//Create background worker.
				BackgroundWorker worker = new BackgroundWorker();
				worker.DoWork += this.btnTest_TestConnection;
				worker.ProgressChanged += this.btnTest_ProgressChanged;
				worker.WorkerReportsProgress = true;
				worker.WorkerSupportsCancellation = false;

				//Disable form..
				this.IsEnabled = false;
				this.isBackgroundRunning = true;

				//Start thread.
				worker.RunWorkerAsync(workArgs);
			}
		}

		/// <summary>Hit when we're going to connect to the server.</summary>
		/// <param name="sender">BackgroundWorker</param>
		/// <param name="e">DoWorkEventArgs</param>
		private void btnTest_TestConnection(object sender, DoWorkEventArgs e)
		{
			BackgroundWorker worker = sender as BackgroundWorker;
			bool success = false;

			if (e.Argument is btnTest_DoWorkArgs)
			{
				//Get our arguments.
				btnTest_DoWorkArgs workerArgs = (btnTest_DoWorkArgs)e.Argument;

				//Start the process..!
				worker.ReportProgress(0, "Connecting to server...");
				if (workerArgs.isSpira)
				{
					try
					{
						//Create client.
						Settings.SpiraClient.ImportExportClient spiraClient = Settings.ClientFactory.CreateClient_Spira(workerArgs.serverUrl);

						//Now try to sign in.
						worker.ReportProgress(50, "Logging in...");
						success = spiraClient.Connection_Authenticate2(workerArgs.loginId, workerArgs.loginPass, "EmailIntegration");
					}
					catch (Exception ex)
					{
						worker.ReportProgress(100, ex);
						return;
					}
				}
				else
				{
					try
					{
						//reate client.
						Settings.KronoClient.ImportExportClient kronoClient = Settings.ClientFactory.CreateClient_Krono(workerArgs.serverUrl);

						//Now try to sign in.
						worker.ReportProgress(50, "Logging in...");
						success = kronoClient.Connection_Authenticate(workerArgs.loginId, workerArgs.loginPass, "EmailIntegration", false);
					}
					catch (Exception ex)
					{
						worker.ReportProgress(100, ex);
						return;
					}
				}

				//Send result back to main thread.
				worker.ReportProgress(100, success);
			}
		}

		/// <summary>Hit when we're updating testing progress.</summary>
		/// <param name="sender">BackgroundWorker</param>
		/// <param name="e">ProgressChangedEventArgs</param>
		private void btnTest_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			//See what we need to do..
			if (e.UserState is bool)
			{
				//Success or failure?
				if ((bool)e.UserState)
				{
					this.lblTestResult.Content = "Success logging in!";
				}
				else
				{
					this.lblTestResult.Content = "Error logging in. Check login & password.";
				}

				//Re-enable buttons.
				this.IsEnabled = true;
				this.isBackgroundRunning = false;
			}
			else if (e.UserState is Exception)
			{
				//Ex eption occured. Display error message.
				string msg = Main.Details_App_ErrorTesting_Msg + Environment.NewLine + Environment.NewLine + ((Exception)e.UserState).Message.Trim();
				if (((Exception)e.UserState).InnerException != null)
					msg += Environment.NewLine + ((Exception)e.UserState).InnerException.Message.Trim();
				MessageBox.Show(msg, Main.Details_App_ErrorTesting_Title, MessageBoxButton.OK, MessageBoxImage.Error);

				//Re-enable buttons.
				this.IsEnabled = true;
				this.isBackgroundRunning = false;
				this.lblTestResult.Content = null;
			}
			else if (e.UserState is String)
			{
				//Display our message.
				this.lblTestResult.Content = (String)e.UserState;
			}
		}

		/// <summary>Hit when the window is trying to be closed.</summary>
		/// <param name="sender">Details_ApplicationServer</param>
		/// <param name="e">CancelEventArgs</param>
		private void window_Closing(object sender, CancelEventArgs e)
		{
			if (this.isBackgroundRunning)
				e.Cancel = true;
		}
		#endregion //Events

		#region Internal Functions
		/// <summary>Loads the given ApplicationSystem data.</summary>
		private void loadData()
		{
			if (this.AppSystem != null)
			{
				//Load the item into the fields.
				this.rdoKrono.IsChecked = (this.AppSystem.ServerType == ApplicationSystem.ApplServerTypeEnum.Krono);
				this.rdoSpira.IsChecked = (this.AppSystem.ServerType == ApplicationSystem.ApplServerTypeEnum.Spira);
				bool isHttps = (this.AppSystem.ServerURL.StartsWith("https://"));
				this.cmbHttps.Text = ((isHttps) ? "https://" : "http://");
				this.txtServerURL.Text = ((isHttps) ? this.AppSystem.ServerURL.Replace("https://", "") : this.AppSystem.ServerURL.Replace("http://", ""));
				this.txtUserName.Text = this.AppSystem.UserID;
				this.txtUserPass.Password = this.AppSystem.UserPassword;
			}
		}

		/// <summary>Saves the user's information into the ApplicationSystem object. Will return true is save successful, or false if verification errors.</summary>
		/// <remarks>Status of data - TRUE all fields are valid, and saved. FALSE one or more fields need attention.</remarks>
		private bool saveData()
		{
			//Check required fields, first.
			bool retValue = this.checkForm();

			if (retValue)
			{
				//Create a new one, if we didn't have one loaded..
				if (this.AppSystem == null)
					this._appSystem = new ApplicationSystem();

				//App Server type..
				this.AppSystem.ServerType = ((this.rdoKrono.IsChecked.Value) ? ApplicationSystem.ApplServerTypeEnum.Krono : ApplicationSystem.ApplServerTypeEnum.Spira);
				this.AppSystem.ServerURL = this.cmbHttps.Text + this.txtServerURL.Text.Trim();
				this.AppSystem.UserID = this.txtUserName.Text.Trim();
				this.AppSystem.UserPassword = this.txtUserPass.Password.Trim();
				if (!this.AppSystem.ServerID.HasValue) this.AppSystem.ServerID = 0;

				//Set flag..
				retValue = true;
			}

			return retValue;
		}

		/// <summary>Checks that all required fields are filled out.</summary>
		/// <returns>True if everything is valid. False otehrwise.</returns>
		/// <param name="showError">If true 9default) will show messagebox if there is an error.</param>
		private bool checkForm(bool showError = true)
		{
			bool retValue = true;

			//Check  server URL.
			if (string.IsNullOrWhiteSpace(this.txtServerURL.Text))
			{
				if (showError)
					MessageBox.Show("You must enter the server's URL.", "Required Fieldes", MessageBoxButton.OK, MessageBoxImage.Error);

				retValue = false;
			}
			else if (string.IsNullOrWhiteSpace(this.txtUserPass.Password) || string.IsNullOrWhiteSpace(this.txtUserName.Text))
			{
				if (showError)
					MessageBox.Show("An application must have a user name and password for an account to use while creating or adding emails to the system.", "Required Fieldes", MessageBoxButton.OK, MessageBoxImage.Error);

				retValue = false;
			}

			return retValue;
		}
		#endregion //Internal Functions

		#region Internal Classes
		/// <summary>used to pass testing information to the background thread.</summary>
		private class btnTest_DoWorkArgs
		{
			/// <summary>Whether the server is Spirateam (true) or KronoDesk (false)</summary>
			public bool isSpira = false;
			/// <summary>The URL to the server.</summary>
			public Uri serverUrl;
			/// <summary>The login ID and Password.</summary>
			public string loginId, loginPass;

			/// <summary>Creates a new instance of the class.</summary>
			/// <param name="isSpiraTeam">Whether the server is Spirateam (true) or KronoDesk (false)</param>
			/// <param name="serverAddr">The URL to the server.</param>
			/// <param name="userId">The login ID.</param>
			/// <param name="userPass">The login password.</param>
			public btnTest_DoWorkArgs(bool isSpiraTeam, Uri serverAddr, string userId, string userPass)
			{
				this.isSpira = isSpiraTeam;
				this.serverUrl = serverAddr;
				this.loginId = userId;
				this.loginPass = userPass;
			}
		}
		#endregion //Internal Classes
	}
}

