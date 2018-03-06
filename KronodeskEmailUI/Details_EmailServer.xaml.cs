using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Inflectra.KronoDesk.Service.Email.Settings;
using Inflectra.KronoDesk.Service.Email.Settings.KronoClient;
using Inflectra.KronoDesk.Service.Email.Settings.SpiraClient;

namespace Inflectra.KronoDesk.Service.Email.UI
{
	/// <summary>Interaction logic for Details_EmailServer.xaml</summary>
	public partial class Details_EmailServer : Window
	{
		/// <summary>The regular expression for the Port textbox.</summary>
		private Regex NumEx = new Regex(@"^\d*$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
		/// <summary>Default port for POP3</summary>
		private static int PORT_POP3 = 110;
		/// <summary>Default port for POP3 over SSL</summary>
		private static int PORT_POP3_SSL = 995;
		/// <summary>Default port for IMAP</summary>
		private static int PORT_IMAP = 143;
		/// <summary>Default port for IMAP over SSL</summary>
		private static int PORT_IMAP_SSL = 993;
		/// <summary>Default port for MAPI</summary>
		private static int PORT_MAPI = 135;


		#region Constructors
		/// <summary>Creates a new instance of the class.</summary>
		public Details_EmailServer()
		{
			this.InitializeComponent();

			this.EMailAccount = null;
			this.AppSystems = new List<ApplicationSystem>();
		}

		/// <summary>Creates a new instance of the class, setting display properties.</summary>
		/// <param name="emailAcct">The Email Account we're updating/creating.</param>
		/// <param name="appServers">A list of all available Application Servers.</param>
		public Details_EmailServer(EmailAccount emailAcct, List<ApplicationSystem> appServers)
		{
			this.InitializeComponent();
			this.AppSystems = appServers;
			this.EMailAccount = emailAcct;
		}
		#endregion //Constructors

		#region Properties
		/// <summary>The Email account we're editing or creating.</summary>
		public EmailAccount EMailAccount
		{
			get
			{
				return this._mailAccount;
			}
			set
			{
				this._mailAccount = value;
				this.loadData();
			}
		}
		private EmailAccount _mailAccount;

		/// <summary>The list of available Application Systems.</summary>
		public List<ApplicationSystem> AppSystems
		{
			set
			{
				this._appSystems = value;
				this.loadApp();
			}
		}
		private List<ApplicationSystem> _appSystems;
		#endregion //Properties

		#region Internal Functions
		/// <summary>Loads data into the fields for display.</summary>
		private void loadData()
		{
			//Load up teh values.
			if (this._mailAccount != null)
			{
				this.txtAccount.Text = this._mailAccount.AccountEmail;
				this.txtServer.Text = this._mailAccount.ServerNameOrIP;
				this.txtPort.Text = this._mailAccount.ServerPort.ToSafeString();
				this.chkUseSSL.IsChecked = this._mailAccount.UseSSL;
				this.txtLogin.Text = this._mailAccount.UserLogin;
				this.txtPassword.Password = this._mailAccount.UserPass;
				this.chkUseRegEx.IsChecked = this._mailAccount.UseRegexToMatch;
				this.chkDeleteFromServer.IsChecked = this._mailAccount.RemoveFromServer;
				this.chkSaveRaw.IsChecked = this._mailAccount.SaveRawEmailAsAttachment;
				this.chkSave3rd.IsChecked = this._mailAccount.Save3rdPartyAsAttachment;
				if (this._mailAccount.ServerType_IMAP)
					this.rdoIMAP.IsChecked = true;
				else if (this._mailAccount.ServerType_MAPI)
					this.rdoMAPI.IsChecked = true;
				else
					this.rdoPOP3.IsChecked = true;

				//Now set the App Server & Project.
				this.loadApp();
			}
		}

		/// <summary>Loads application servers and sets any pre-defined values.</summary>
		private void loadApp()
		{
			//Load the list..
			this.cmbApplicationServer.SelectedItem = null;
			this.cmbApplicationServer.ItemsSource = this._appSystems;
			this.cmbApplicationServer.Items.Refresh();

			//Now select the one, if we have an account listed.
			if (this._mailAccount != null)
				this.cmbApplicationServer.SelectedItem = this._appSystems.Where(ap => ap.ServerID == this._mailAccount.ApplicationServerID).SingleOrDefault();
		}

		/// <summary>Save the form data.</summary>
		/// <returns>Saving success.</returns>
		private bool saveData()
		{
			bool retvalue = this.checkForm();

			if (retvalue)
			{
				//Set default port..
				if (string.IsNullOrWhiteSpace(this.txtPort.Text))
				{
					if (this.rdoMAPI.IsChecked.HasValue && this.rdoMAPI.IsChecked.Value)
					{
						this.txtPort.Text = PORT_MAPI.ToString();
					}
					else if (this.rdoPOP3.IsChecked.HasValue && this.rdoPOP3.IsChecked.Value)
					{
						this.txtPort.Text = ((this.chkUseSSL.IsChecked.HasValue && this.chkUseSSL.IsChecked.Value) ? PORT_POP3_SSL.ToString() : PORT_POP3.ToString());
					}
					else if (this.rdoIMAP.IsChecked.HasValue && this.rdoIMAP.IsChecked.Value)
					{
						this.txtPort.Text = ((this.chkUseSSL.IsChecked.HasValue && this.chkUseSSL.IsChecked.Value) ? PORT_IMAP_SSL.ToString() : PORT_IMAP.ToString());
					}
				}

				if (retvalue)
				{
					if (this.EMailAccount == null)
					{
						this._mailAccount = new EmailAccount();
						this._mailAccount.AccountID = 0;
					}

					//Work down the form.
					this._mailAccount.AccountEmail = this.txtAccount.Text.Trim();
					if (this.rdoPOP3.IsChecked.Value)
					{
						this._mailAccount.ServerType_IMAP = false;
						this._mailAccount.ServerType_MAPI = false;
					}
					else if (this.rdoMAPI.IsChecked.Value)
					{
						this._mailAccount.ServerType_MAPI = true;
						this._mailAccount.ServerType_IMAP = false;
					}
					else if (this.rdoIMAP.IsChecked.Value)
					{
						this._mailAccount.ServerType_MAPI = false;
						this._mailAccount.ServerType_IMAP = true;
					}
					this._mailAccount.ServerNameOrIP = this.txtServer.Text.Trim();
					this._mailAccount.ServerPort = int.Parse(this.txtPort.Text.Trim());
					this._mailAccount.UseSSL = this.chkUseSSL.IsChecked.Value;
					this._mailAccount.UserLogin = this.txtLogin.Text.Trim();
					this._mailAccount.UserPass = this.txtPassword.Password;
					this._mailAccount.ApplicationServerID = ((ApplicationSystem)this.cmbApplicationServer.SelectedItem).ServerID;
					if (this.cmbSelectedProjectOrProduct.SelectedItem is RemoteProject)
					{
						this._mailAccount.ProductOrProjectId = ((RemoteProject)this.cmbSelectedProjectOrProduct.SelectedItem).ProjectId.Value;
					}
					else if (this.cmbSelectedProjectOrProduct.SelectedItem is RemoteProduct)
					{
						this._mailAccount.ProductOrProjectId = Convert.ToInt32(((RemoteProduct)this.cmbSelectedProjectOrProduct.SelectedItem).ProductId.Value);
					}
					else
					{
						retvalue = false;
						MessageBox.Show("", "", MessageBoxButton.OK, MessageBoxImage.Error);
					}
					this._mailAccount.UseRegexToMatch = this.chkUseRegEx.IsChecked.Value;
					this._mailAccount.RemoveFromServer = this.chkDeleteFromServer.IsChecked.Value;
					this._mailAccount.SaveRawEmailAsAttachment = this.chkSaveRaw.IsChecked.Value;
					this._mailAccount.Save3rdPartyAsAttachment = this.chkSave3rd.IsChecked.Value;
				}
			}

			//Return the value.
			return retvalue;
		}

		/// <summary>Checks for valid entry data.</summary>
		/// <returns></returns>
		/// <param name="showError">If true (Default), show messagebox to user.</param>
		private bool checkForm(bool showError = true)
		{
			bool retValue = true;

			//First check required datas.
			// - Email Address
			if (string.IsNullOrWhiteSpace(this.txtAccount.Text))
			{
				if (showError)
					MessageBox.Show(Main.Details_Email_RequiredAddress_Msg, Main.Details_Email_Required_Title, MessageBoxButton.OK, MessageBoxImage.Error);

				retValue = false;
			}
			// - Server IP.
			else if (string.IsNullOrWhiteSpace(this.txtServer.Text))
			{
				if (showError)
					MessageBox.Show(Main.Details_Email_RequiredServer_Msg, Main.Details_Email_Required_Title, MessageBoxButton.OK, MessageBoxImage.Error);

				retValue = false;
			}
			// - App Server & Default Product/Project
			else if (this.cmbApplicationServer.SelectedIndex < 0 || this.cmbSelectedProjectOrProduct.SelectedIndex < 0)
			{
				if (showError)
					MessageBox.Show(Main.Details_Email_RequiredProject_Msg, Main.Details_Email_Required_Title, MessageBoxButton.OK, MessageBoxImage.Error);

				retValue = false;
			}

			return retValue;
		}

		/// <summary>Sets the controls for them to be 'hidden' by the info panel.</summary>
		/// <param name="hidden">True if they are hidden, false otherwise.</param>
		private void markControlsAsHidden(bool hidden)
		{
			float setOpc = ((hidden) ? .20F : 1);

			//Visibility..
			this.lblDefault.Opacity = setOpc;
			this.cmbSelectedProjectOrProduct.Opacity = setOpc;
			this.cmbApplicationServer.Opacity = setOpc;
			this.lblAppServer.Opacity = setOpc;
			//Enabled-ity?
			//this.btnEmDel.IsEnabled = (!hidden);
			this.cmbSelectedProjectOrProduct.IsEnabled = (!hidden);
			this.cmbApplicationServer.IsEnabled = (!hidden);
		}
		#endregion //Internal Functions

		#region Events
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

		/// <summary>Hit when the application server selection is changed. Fires a background event to load up projects or products.</summary>
		/// <param name="sender">cmbApplicationServer</param>
		/// <param name="e">SelectionChangedEventArgs</param>
		private void cmbApplicationServer_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			//Mark controls as hidden...
			this.markControlsAsHidden(true);

			//Now fire off a background thread if they selected a server.
			if (e.AddedItems.Count == 1)
			{
				//We have to fire off the client and get a list of products or projects.
				ApplicationSystem selectedApp = (ApplicationSystem)e.AddedItems[0];

				//Set common stuff..
				this.Cursor = Cursors.Wait;
				this.cmbSelectedProjectOrProduct.IsEnabled = false;
				this.divUpdating.Visibility = System.Windows.Visibility.Visible;
				this.txtUpdating.Foreground = Brushes.Black;
				this.spnSpinner.Visibility = System.Windows.Visibility.Visible;

				if (selectedApp.ServerType == ApplicationSystem.ApplServerTypeEnum.Spira)
				{
					//Set cursor and disable product/project dropdown..
					this.lblDefault.Content = "Default Project:";
					this.txtUpdating.Text = "Loading SpiraTeam projects...";

					//Start the thread..
					Thread_Spira_GetProjects proc = new Thread_Spira_GetProjects(selectedApp.ServerURL, selectedApp.UserID, selectedApp.UserPassword);
					proc.ProgressFinished += new EventHandler<SpiraFinishArgs>(proc_ProgressFinished);
					Thread thread = new Thread(proc.StartProcess);
					thread.IsBackground = true;
					thread.Name = "Thread - Get SpiraTeam Projects";
					thread.Start();

					//Disable the default project & use regex.
					//this.Cursor = Cursors.Arrow;
				}
				else if (selectedApp.ServerType == ApplicationSystem.ApplServerTypeEnum.Krono)
				{
					//Set cursor and disable product/project dropdown..
					this.lblDefault.Content = Common.LABEL_DEFAULT_PRODUCT;
					this.txtUpdating.Text = "Loading KronoDesk products from server...";

					//Start the thread..
					Thread_Krono_GetProducts proc = new Thread_Krono_GetProducts(selectedApp.ServerURL, selectedApp.UserID, selectedApp.UserPassword);
					proc.ProgressFinished += new EventHandler<KronoFinishArgs>(proc_ProgressFinishedKrono);
					Thread thread = new Thread(proc.StartProcess);
					thread.IsBackground = true;
					thread.Name = "Thread - Get KronoDesk Products";
					thread.Start();
				}
			}

			e.Handled = true;
		}

		/// <summary>Handles text entry into the textbox.</summary>
		/// <param name="sender">txtPort</param>
		/// <param name="e">TextCompositionEventArgs</param>
		private void txtPort_PreviewTextInput(object sender, TextCompositionEventArgs e)
		{
			if (this.IsInitialized && sender is TextBox)
			{
				string text = (sender as TextBox).Text + e.Text;
				e.Handled = !NumEx.IsMatch(text);
			}
		}

		/// <summary>Hit when the mail server type is changed.</summary>
		/// <param name="sender">rdoPOP3, rdoIMAP, rdoMAPIP</param>
		/// <param name="e">RoutedEventArgs</param>
		private void rdoType_Checked(object sender, RoutedEventArgs e)
		{
			if (this.IsInitialized)
			{
				//Figure out which one is selected.
				if (this.rdoPOP3.IsChecked.Value)
					this.txtPort.Text = ((this.chkUseSSL.IsChecked.Value) ? PORT_POP3_SSL.ToString() : PORT_POP3.ToString());
				else if (this.rdoIMAP.IsChecked.Value)
					this.txtPort.Text = ((this.chkUseSSL.IsChecked.Value) ? PORT_IMAP_SSL.ToString() : PORT_IMAP.ToString());
				else if (this.rdoMAPI.IsChecked.Value)
					this.txtPort.Text = PORT_MAPI.ToString();

				e.Handled = true;
			}
		}

		/// <summary>Delegate for when the thread finishes.</summary>
		/// <param name="sender">Thread_Spira_GetProjects</param>
		/// <param name="e">FinishArgs</param>
		private delegate void proc_ProgressFinishedCallback(object sender, SpiraFinishArgs e);

		/// <summary>Delegate for when the thread finishes.</summary>
		/// <param name="sender">Thread_Spira_GetProjects</param>
		/// <param name="e">FinishArgs</param>
		private delegate void proc_ProgressFinishedKronoCallback(object sender, KronoFinishArgs e);

		/// <summary>Hit when we're finished getting results from SpiraTeam.</summary>
		/// <param name="sender">Thread_Spira_GetProjects</param>
		/// <param name="e">FinishArgs</param>
		private void proc_ProgressFinished(object sender, SpiraFinishArgs e)
		{
			if (this.Dispatcher.CheckAccess())
			{
				if (e.Error == null)
				{
					//We connected successfully.
					this.cmbSelectedProjectOrProduct.IsEnabled = true;
					this.cmbSelectedProjectOrProduct.ItemsSource = e.Projects;

					//Set selected item..
					if (this._mailAccount != null)
						this.cmbSelectedProjectOrProduct.SelectedItem = e.Projects.Where(prj => prj.ProjectId == this._mailAccount.ProductOrProjectId).SingleOrDefault();

					//Hide the banner.
					this.divUpdating.Visibility = System.Windows.Visibility.Collapsed;
					this.markControlsAsHidden(false);
				}
				else
				{
					//Remove the spinner and display error message..
					this.spnSpinner.Visibility = System.Windows.Visibility.Collapsed;
					this.txtUpdating.Text = e.Error.Message;
					this.txtUpdating.Foreground = Brushes.DarkRed;
				}

				//Reset the cursor, no matter what.
				this.Cursor = Cursors.Arrow;
			}
			else
			{
				proc_ProgressFinishedCallback callB = new proc_ProgressFinishedCallback(this.proc_ProgressFinished);
				this.Dispatcher.Invoke(callB, new object[] { sender, e });
			}
		}

		/// <summary>Hit when we're finished getting results from SpiraTeam.</summary>
		/// <param name="sender">Thread_Spira_GetProjects</param>
		/// <param name="e">FinishArgs</param>
		private void proc_ProgressFinishedKrono(object sender, KronoFinishArgs e)
		{
			if (this.Dispatcher.CheckAccess())
			{
				if (e.Error == null)
				{
					//Enable controls, hid banner..
					this.divUpdating.Visibility = System.Windows.Visibility.Collapsed;
					this.markControlsAsHidden(false);

					//We connected successfully.
					this.cmbSelectedProjectOrProduct.ItemsSource = e.Products;

					//Set selected item..
					if (this._mailAccount != null)
						this.cmbSelectedProjectOrProduct.SelectedItem = e.Products.Where(prd => prd.ProductId == this._mailAccount.ProductOrProjectId).SingleOrDefault();
				}
				else
				{
					//Remove the spinner and display error message..
					this.spnSpinner.Visibility = System.Windows.Visibility.Collapsed;
					this.txtUpdating.Text = e.Error.Message;
					this.txtUpdating.Foreground = Brushes.DarkRed;
				}

				//Reset the cursor, no matter what.
				this.Cursor = Cursors.Arrow;
			}
			else
			{
				proc_ProgressFinishedKronoCallback callB = new proc_ProgressFinishedKronoCallback(this.proc_ProgressFinishedKrono);
				this.Dispatcher.Invoke(callB, new object[] { sender, e });
			}
		}
		#endregion //Events
	}
}
