using System.Windows;
using System.Windows.Controls;
using Inflectra.KronoDesk.Service.Email.Settings;
using System.IO;
using System.Reflection;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Inflectra.KronoDesk.Service.Email.UI
{
	/// <summary>Interaction logic for MainWindow.xaml</summary>
	public partial class MainWindow : Window
	{
		/// <summary>The settings file.</summary>
		SettingsStorage_100 settings = null;

		/// <summary>Constructor.</summary>
		public MainWindow()
		{
			InitializeComponent();

			//Load settings file here.
			this.settingsLoadFromFile();
			this.settingsLoadToForm();
		}

		#region Form Events
		/// <summary>Hit when the user wants to save the config file.</summary>
		/// <param name="sender">btnSave</param>
		/// <param name="e">RoutedEventArgs</param>
		private void btnSave_Clicked(object sender, RoutedEventArgs e)
		{
			//Save the loaded settings into the file.
			this.settingsSaveToFile();
		}

		/// <summary>Hit when either one of the user controls needs to save data to the config file.</summary>
		/// <param name="sender">cntEmailServer, cntApplicationServer</param>
		/// <param name="e">EventArgs</param>
		private void cntItemChanged_AccountChanged(object sender, EventArgs e)
		{
			//Set the flag on the window.
			this.WindowSetUnsaved(true);
		}

		/// <summary>Hit when the tab control is changing.</summary>
		/// <param name="sender">TabControl</param>
		/// <param name="e">SelectionChangedEventArgs</param>
		private void tabPanel_Changed(object sender, SelectionChangedEventArgs e)
		{
			//Inform the two panels that things changed.
			//this.cntApplicationServer.WindowIsChanging();
			//this.cntEmailServer.WindowIsChanging();
		}

		/// <summary>Hit when the window is going to be closed. Checks to make sure there are no unsaved changes.</summary>
		/// <param name="sender">Window</param>
		/// <param name="e">CancelEventArgs</param>
		private void window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			//If we need to save changes, ask the user if they want to close, save, or cancel.
			if (this.btnCommit.IsEnabled)
			{
				//Ask..
				MessageBoxResult answer = MessageBox.Show("You have unsaved changes! Do you want to save before exiting?", "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Yes);

				if (answer == MessageBoxResult.Cancel)
					e.Cancel = true;
				else if (answer == MessageBoxResult.Yes)
					this.settingsSaveToFile();
			}
		}

		/// <summary>Hit when the user enabled TraceLogging.</summary>
		/// <param name="sender">CheckBox</param>
		/// <param name="e">RoutedEventArgs</param>
		private void chkTrace_Checked(object sender, RoutedEventArgs e)
		{
			e.Handled = true;
			if (this.chkTrace.IsChecked.Value && !this.settings.EnableTrace)
			{
				MessageBoxResult ans = MessageBoxResult.No;
				ans = MessageBox.Show("Enabling this could quickly fill up the system's Application Event Log, and affect system performance. It is recommended only to enable this at support's request." + Environment.NewLine + Environment.NewLine + "Are you sure you wish to enable it?", "Enabling Trace Logging", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

				if (ans != MessageBoxResult.Yes)
					this.chkTrace.IsChecked = false;
			}

			this.WindowSetUnsaved(true);
		}

		/// <summary>Hit when the text control gets a character.</summary>
		/// <param name="sender">txtMinutes</param>
		/// <param name="e">TextCompositionEventArgs</param>
		private void TextBox_PreviewTextInput_IsNumber(object sender, System.Windows.Input.TextCompositionEventArgs e)
		{
			e.Handled = !e.Text.All(Char.IsNumber);
			base.OnPreviewTextInput(e);
		}

		/// <summary>Hit when the user changed information in the New Ingore Email box.</summary>
		/// <param name="sender">txtNewIgnEmail</param>
		/// <param name="e">TextChangedEventArgs</param>
		private void txtNewIgnEmail_TextChanged(object sender, TextChangedEventArgs e)
		{
			//Enable or disable the add button.
			this.btnAddIgnEmail.IsEnabled = !string.IsNullOrWhiteSpace(this.txtNewIgnEmail.Text.Trim());
		}

		/// <summary>Hit when the user wants to add an email to the list.</summary>
		/// <param name="sender">brnAddIgnEmail</param>
		/// <param name="e">RoutedEventArgs</param>
		private void btnAddIgnEmail_Click(object sender, RoutedEventArgs e)
		{
			//Ge tthe string and add it to the list.
			this.settings.Ignore_Addresses.Add(this.txtNewIgnEmail.Text.Trim());
			//Refresh the list.
			this.settings.Ignore_Addresses.Sort();
			this.lstIgnoreEmails.ItemsSource = this.settings.Ignore_Addresses;
			this.lstIgnoreEmails.Items.Refresh();
			//Clear out the textbox.
			this.txtNewIgnEmail.Text = "";

			e.Handled = true;
			this.WindowSetUnsaved(true);
		}

		/// <summary>Hit when the user wants to remove the selected emails.</summary>
		/// <param name="sender">btnDelIgnEmail</param>
		/// <param name="e">RoutedEventArgs</param>
		private void btnDelIgnEmail_Click(object sender, RoutedEventArgs e)
		{
			foreach (object selItem in this.lstIgnoreEmails.SelectedItems)
			{
				//Remove it from the list.
				this.settings.Ignore_Addresses.Remove((string)selItem);
			}

			//Refresh the list.
			this.lstIgnoreEmails.ItemsSource = this.settings.Ignore_Addresses;
			this.settings.Ignore_Addresses.Sort();
			this.lstIgnoreEmails.Items.Refresh();

			e.Handled = true;
			this.WindowSetUnsaved(true);
		}

		/// <summary>Enable or disable the delete button.</summary>
		/// <param name="sender">lstIgnoreEmails</param>
		/// <param name="e">SelectionChangedEventArgs</param>
		private void lstIgnoreEmails_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			this.btnDelIgnEmail.IsEnabled = (this.lstIgnoreEmails.SelectedItems.Count > 0);

			e.Handled = true;
		}

		/// <summary>Hit when the user changed information in the New Ingore Email box.</summary>
		/// <param name="sender">txtNewIgnHeader</param>
		/// <param name="e">TextChangedEventArgs</param>
		private void txtNewIgnHeader_TextChanged(object sender, TextChangedEventArgs e)
		{
			//Enable or disable the add button.
			this.btnAddIgnHeader.IsEnabled = !string.IsNullOrWhiteSpace(this.txtNewIgnHeader.Text.Trim());
		}

		/// <summary>Hit when the user wants to add an email to the list.</summary>
		/// <param name="sender">brnAddIgnHeader</param>
		/// <param name="e">RoutedEventArgs</param>
		private void btnAddTgnHeader_Click(object sender, RoutedEventArgs e)
		{
			//Ge tthe string and add it to the list.
			this.settings.Ignore_Headers.Add(this.txtNewIgnHeader.Text.Trim());
			//Refresh the list.
			this.settings.Ignore_Headers.Sort();
			this.lstIgnoreHeaders.ItemsSource = this.settings.Ignore_Headers;
			this.lstIgnoreHeaders.Items.Refresh();
			//Clear out the textbox.
			this.txtNewIgnHeader.Text = "";

			e.Handled = true;
			this.WindowSetUnsaved(true);
		}

		/// <summary>Enable or disable the delete button.</summary>
		/// <param name="sender">lstIgnoreHeaders</param>
		/// <param name="e">SelectionChangedEventArgs</param>
		private void lstIgnoreHeader_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			this.btnDelIgnHeader.IsEnabled = (this.lstIgnoreHeaders.SelectedItems.Count > 0);

			e.Handled = true;
		}

		/// <summary>Hit when the user wants to remove the selected emails.</summary>
		/// <param name="sender">btnDelIgnHeader</param>
		/// <param name="e">RoutedEventArgs</param>
		private void btnDelIgnHeader_Click(object sender, RoutedEventArgs e)
		{
			foreach (object selItem in this.lstIgnoreHeaders.SelectedItems)
			{
				//Remove it from the list.
				this.settings.Ignore_Headers.Remove((string)selItem);
			}

			//Refresh the list.
			this.settings.Ignore_Headers.Sort();
			this.lstIgnoreHeaders.ItemsSource = this.settings.Ignore_Headers;
			this.lstIgnoreHeaders.Items.Refresh();

			e.Handled = true;
			this.WindowSetUnsaved(true);
		}

		/// <summary>Hit when the user wants to add an email to the list.</summary>
		/// <param name="sender">brnAddIgnHeader</param>
		/// <param name="e">RoutedEventArgs</param>
		private void btnAddIgnWord_Click(object sender, RoutedEventArgs e)
		{
			//Ge tthe string and add it to the list.
			this.settings.Ignore_Words.Add(this.txtNewIgnWord.Text.Trim());
			//Refresh the list.
			this.settings.Ignore_Words.Sort();
			this.lstIgnoreWords.ItemsSource = this.settings.Ignore_Words;
			this.lstIgnoreWords.Items.Refresh();
			//Clear out the textbox.
			this.txtNewIgnWord.Text = "";

			e.Handled = true;
			this.WindowSetUnsaved(true);
		}

		/// <summary>Enable or disable the delete button.</summary>
		/// <param name="sender">lstIgnoreHeaders</param>
		/// <param name="e">SelectionChangedEventArgs</param>
		private void lstIgnoreWords_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			this.btnDelIgnWord.IsEnabled = (this.lstIgnoreWords.SelectedItems.Count > 0);

			e.Handled = true;
		}

		/// <summary>Hit when the user wants to remove the selected emails.</summary>
		/// <param name="sender">btnDelIgnHeader</param>
		/// <param name="e">RoutedEventArgs</param>
		private void btnDelIgnWord_Click(object sender, RoutedEventArgs e)
		{
			foreach (object selItem in this.lstIgnoreWords.SelectedItems)
			{
				//Remove it from the list.
				this.settings.Ignore_Words.Remove((string)selItem);
			}

			//Refresh the list.
			this.settings.Ignore_Words.Sort();
			this.lstIgnoreWords.ItemsSource = this.settings.Ignore_Words;
			this.lstIgnoreWords.Items.Refresh();

			e.Handled = true;
			this.WindowSetUnsaved(true);
		}

		/// <summary>Hit when the user changed information in the New Ingore Email box.</summary>
		/// <param name="sender">txtNewIgnHeader</param>
		/// <param name="e">TextChangedEventArgs</param>
		private void txtNewIgnWord_TextChanged(object sender, TextChangedEventArgs e)
		{
			//Enable or disable the add button.
			this.btnAddIgnWord.IsEnabled = !string.IsNullOrWhiteSpace(this.txtNewIgnWord.Text.Trim());
		}

		/// <summary>Hit when the user changes wether they want to enable an empty Return-Path header.</summary>
		/// <param name="sender">Checkbox</param>
		/// <param name="e">RoutedEventArgs</param>
		private void CheckBox_Checked(object sender, RoutedEventArgs e)
		{
			e.Handled = true;
			this.WindowSetUnsaved(true);
		}

		/// <summary>
		/// Checks to make sure everything has been saved and then closes the application
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnClose_Click(object sender, RoutedEventArgs e)
		{
			//If we need to save changes, ask the user if they want to close, save, or cancel.
			if (this.btnCommit.IsEnabled)
			{
				//Ask..
				MessageBoxResult answer = MessageBox.Show("You have unsaved changes! Do you want to save before exiting?", "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Yes);

				if (answer == MessageBoxResult.Cancel)
					return;
				else if (answer == MessageBoxResult.Yes)
					this.settingsSaveToFile();
				Application.Current.Shutdown();
			}
			else
			{
				Application.Current.Shutdown();
			}
		}

		/// <summary>Hit when the user wants to add a new EMail Account.</summary>
		/// <param name="sender">btnAddEmail</param>
		/// <param name="e">RoutedEventArgs</param>
		private void btnAddEmail_Click(object sender, RoutedEventArgs e)
		{
			Details_EmailServer win = new Details_EmailServer(null, this.settings.Application_Servers);
			bool? resp = win.ShowDialog();
			if (resp.HasValue && resp.Value)
			{
				//Save the application server!
				this.AddOrUpdateEmailAccount(win.EMailAccount);
			}
		}

		/// <summary>Hit whwen the user wants to delete a configured EMail account.</summary>
		/// <param name="sender">btnDelEmail</param>
		/// <param name="e"><RoutedEventArgs/param>
		private void btnDelEmail_Click(object sender, RoutedEventArgs e)
		{
			//TODO: Verify that the user wants to delete the email account, then nuke it.
		}

		/// <summary>Hit when the selection in the Listbox was changed.</summary>
		/// <param name="sender">lstAccounts</param>
		/// <param name="e">SelectionChangedEventArgs</param>
		private void lstEmailAccounts_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			//Only enable the delete button if more than one is selected.
			this.btnDelEmail.IsEnabled = (this.lstEmailAccounts.SelectedItems.Count > 0);
		}

		/// <summary>Hit when the selectrion in the Listbox was changed.</summary>
		/// <param name="sender">lstServers</param>
		/// <param name="e">SelectionChangedEventArgs</param>
		private void lstApplicationServers_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			//Enable the Delete button if there's one selected.
			this.btnDelApp.IsEnabled = (this.lstApplicationServers.SelectedItems.Count == 1);
		}

		/// <summary>Hit when the user wants to add a new Application Server.</summary>
		/// <param name="sender">btnAddApp</param>
		/// <param name="e"></param>
		private void btnAddApp_Click(object sender, RoutedEventArgs e)
		{
			Details_ApplicationServer win = new Details_ApplicationServer();
			bool? resp = win.ShowDialog();
			if (resp.HasValue && resp.Value)
			{
				//Save the application server!
				this.AddOrUpdateApplicationSystem(win.AppSystem);
			}
		}

		/// <summary>Hit when the user wants to delete the selected Aopplication Server.</summary>
		/// <param name="sender">btnDelAcct</param>
		/// <param name="e">RoutedEventArgs</param>
		private void btnDelApp_Click(object sender, RoutedEventArgs e)
		{
			if (this.lstApplicationServers.SelectedItem is ApplicationSystem)
			{
				//Ask if they're sure..
				MessageBoxResult ans = MessageBox.Show(Main.Main_DeleteApp_AreYouSure_Msg, Main.Main_DeleteApp_AreYouSure_Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
				if (ans == MessageBoxResult.Yes)
				{
					//Remove the server.
					ApplicationSystem system = (ApplicationSystem)this.lstApplicationServers.SelectedItem;
					this.settings.Application_Servers.Remove(system);

					//Go through each email account, and if it uses this app server, set it to null.
					foreach (EmailAccount acct in this.settings.Email_Accounts)
					{
						if (acct.ApplicationServerID.HasValue && acct.ApplicationServerID.Value == system.ServerID.Value)
						{
							acct.ApplicationServerID = null;
						}
					}

					//Refresh list.
					this.lstApplicationServers.Items.Refresh();
				}
			}
		}

		/// <summary>Hit once the window is drawn. Used to turn off auto-resizing.</summary>
		/// <param name="e">EventArgs</param>
		protected override void OnContentRendered(EventArgs e)
		{
			base.OnContentRendered(e);

			this.SizeToContent = System.Windows.SizeToContent.Manual;
		}

		/// <summary>Hit when the user wants to edit an existing application.</summary>
		/// <param name="sender">lstApplicationServers.Item</param>
		/// <param name="e"></param>
		private void lstApplicationServersItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ClickCount == 2)
			{
				//Get the data item..
				if (sender is Grid)
				{
					if (((Grid)sender).DataContext is ApplicationSystem)
					{
						ApplicationSystem appSys = (ApplicationSystem)((Grid)sender).DataContext;

						//Display the form..
						Details_ApplicationServer win = new Details_ApplicationServer(appSys);
						bool? resp = win.ShowDialog();
						if (resp.HasValue && resp.Value)
						{
							//Add or update the list..
							this.AddOrUpdateApplicationSystem(appSys);
						}
					}
				}
			}
		}

		/// <summary>Hit when the user wants to edit an existing item.</summary>
		/// <param name="sender">lstEmailAccounts</param>
		/// <param name="e">MouseButtonEventArgs</param>
		private void lstEmailAccountsItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ClickCount == 2)
			{
				//Get the data item..
				if (sender is StackPanel)
				{
					if (((StackPanel)sender).DataContext is EmailAccount)
					{
						EmailAccount emAcct = (EmailAccount)((StackPanel)sender).DataContext;

						//Display the form..
						Details_EmailServer win = new Details_EmailServer(emAcct, this.settings.Application_Servers);
						bool? resp = win.ShowDialog();
						if (resp.HasValue && resp.Value)
						{
							//Add or update the list..
							this.AddOrUpdateEmailAccount(emAcct);
						}
					}
				}
			}
		}

		/// <summary>Hit when a textbox's text is changed. For textboxes that require no special handling.</summary>
		/// <param name="sender">TextBox</param>
		/// <param name="e">TextChangedEventArgs</param>
		private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			this.WindowSetUnsaved(true);
			e.Handled = true;
		}

		/// <summary>Hit when the user enables/disables SpamAssassin options.</summary>
		/// <param name="sender">chkUseSpamAssassin</param>
		/// <param name="e">RoutedEventArgs</param>
		private void chkUseSpamAssassin_Checked(object sender, RoutedEventArgs e)
		{
			//Set window to unsaved.
			this.WindowSetUnsaved(true);

			//Mark fields as being inaccessible.
			bool isEnabled = (this.chkUseSpamAssassin.IsChecked.HasValue && this.chkUseSpamAssassin.IsChecked.Value);
			this.lblSpamAss_ServerNamePort.IsEnabled = isEnabled;
			this.txtSpamAss_ServerName.IsEnabled = isEnabled;
			this.txtSpamAss_ServerPort.IsEnabled = isEnabled;
			this.lblSpamAss_UseSASpam.IsEnabled = isEnabled;
			this.chkSpamAss_UseCustomSpamLevel.IsEnabled = isEnabled;
			this.lblSpamAss_CustomSALevel.IsEnabled = isEnabled;
			this.txtSpamAss_CustomSALevel.IsEnabled = isEnabled;

			e.Handled = true;
		}

		/// <summary>Hit when the user changes their spam settings.</summary>
		/// <param name="sender">chkSpamAss_UseSASpam</param>
		/// <param name="e">RoutedEventArgs</param>
		private void chkSpamAss_UseCustomSpamLevel_Checked(object sender, RoutedEventArgs e)
		{
			//Set window to unsaved.
			this.WindowSetUnsaved(true);

			//Mark fields as being inaccessible.
			bool isEnabled = (this.chkSpamAss_UseCustomSpamLevel.IsChecked.HasValue && this.chkSpamAss_UseCustomSpamLevel.IsChecked.Value);
			this.lblSpamAss_CustomSALevel.IsEnabled = isEnabled;
			this.txtSpamAss_CustomSALevel.IsEnabled = isEnabled;

			e.Handled = true;
		}
		#endregion //Events

		/// <summary>Set the 'unsaved' tick or not.</summary>
		/// <param name="unsaved">True to display the asterisk, false to remove it.</param>
		private void WindowSetUnsaved(bool unsaved)
		{
			//Set the main 'Commit' button..
			this.btnCommit.IsEnabled = unsaved;

			if (unsaved)
			{
				if (!this.Title.EndsWith(" *"))
				{
					this.Title += " *";
				}
			}
			else
			{
				this.Title = this.Title.Replace(" *", "");
			}
		}

		/// <summary>Loads the settings from the specified file.</summary>
		private void settingsLoadFromFile()
		{
			try
			{
				string settingsDir = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Common.APP_MANUF + "\\" + Common.APP_DIRECTORY);
				string fileName = Path.Combine(settingsDir, Common.SETTINGS_FILE);

				if (File.Exists(fileName))
				{
					//Load the XML.
					XmlSerializer xmlSer = new XmlSerializer(typeof(SettingsStorage_100));
					using (FileStream fileStream = new FileStream(fileName, FileMode.Open))
						this.settings = (SettingsStorage_100)xmlSer.Deserialize(fileStream);
				}
				else
				{
					//Need to create a new settings file. The message box was for debugging purposes
					//MessageBox.Show("Configuration file not found. Creating new file!", "New Installation", MessageBoxButton.OK, MessageBoxImage.Information);

					//Create a new settings file.
					this.settings = new SettingsStorage_100();
					this.settings.System_Initialized = true;
					this.WindowSetUnsaved(true);
				}
			}
			catch (Exception ex)
			{
				//Show messagebox.
				MessageBox.Show("There was an error initializing the program:" + Environment.NewLine + ex.Message + Environment.NewLine + Environment.NewLine + "The application will close.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				this.Close();
			}
		}

		/// <summary>Save the loaded in-memory settings to a file.</summary>
		private void settingsSaveToFile()
		{
			//Advanced
			this.settings.EnableTrace = this.chkTrace.IsChecked.Value;
			this.settings.EmptyReturnPath = this.chkAllowEmptyRetPath.IsChecked.Value;
			int minPoll;
			if (int.TryParse(this.txtMinutes.Text.Trim(), out minPoll))
				this.settings.PollInterval = minPoll;
			else
				this.settings.PollInterval = Common.SETTINGS_DEFAULT_POLLINTERVAL;

			//Advanced - Ignore Addresses
			this.settings.Ignore_Addresses = this.lstIgnoreEmails.Items.OfType<string>().ToList<string>();

			//Advanced - Ignore Headers
			this.settings.Ignore_Headers = this.lstIgnoreHeaders.Items.OfType<string>().ToList<string>();

			//Advanced - Ignore Keywords
			this.settings.Ignore_Words = this.lstIgnoreWords.Items.OfType<string>().ToList<string>();

			//Advanced - Spam Assassin
			int saPort = 0;
			double? saLevel = null;
			// - Check that the checkbox is tripped, and if so, get the real port number. If not, clear boxes.
			if (this.chkUseSpamAssassin.IsChecked.HasValue && this.chkUseSpamAssassin.IsChecked.Value)
			{
				//Check server entry..
				if (string.IsNullOrWhiteSpace(this.txtSpamAss_ServerName.Text))
				{
					this.chkUseSpamAssassin.IsChecked = false;
					this.txtSpamAss_ServerPort.Text = "";
				}
				else
				{
					//Check the port entry, first.
					if (string.IsNullOrWhiteSpace(this.txtSpamAss_ServerPort.Text)) this.txtSpamAss_ServerPort.Text = "783";
					if (!int.TryParse(this.txtSpamAss_ServerPort.Text, out saPort))
					{
						this.txtSpamAss_ServerPort.Text = "783";
						saPort = 783;
					}
				}
			}
			// - Check that they have a custom level defined, if the checkbox is checked.
			if (this.chkSpamAss_UseCustomSpamLevel.IsChecked.HasValue && this.chkSpamAss_UseCustomSpamLevel.IsChecked.Value)
			{
				if (string.IsNullOrWhiteSpace(this.txtSpamAss_CustomSALevel.Text)) //Make sure it's not empty.
					this.chkSpamAss_UseCustomSpamLevel.IsChecked = false;
				else
				{
					double saCheckLevel = 0.0;
					if (double.TryParse(this.txtSpamAss_CustomSALevel.Text, out saCheckLevel)) //And it can be converted into a double.
						saLevel = saCheckLevel;
					else //It couldn't, revert settings.
					{
						//Error translating, revert back.
						this.chkSpamAss_UseCustomSpamLevel.IsChecked = false;
						this.txtSpamAss_CustomSALevel.Text = "";
						saLevel = null;
					}
				}
			}
			else
			{
				this.txtSpamAss_CustomSALevel.Text = "";
			}
			this.settings.UseSpamAssassin = this.chkUseSpamAssassin.IsChecked.Value;
			this.settings.SpamAssassinServer = this.txtSpamAss_ServerName.Text.Trim();
			this.settings.SpamAssassinPort = saPort;
			this.settings.UseCustomSALevel = this.chkSpamAss_UseCustomSpamLevel.IsChecked.Value;
			this.settings.CustomSALevel = saLevel;

			//Get the directory we're storing the file in.
			string settingsDir = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Common.APP_MANUF + "\\" + Common.APP_DIRECTORY);
			if (!Directory.Exists(settingsDir))
			{
				Directory.CreateDirectory(settingsDir);
			}
			string fileName = Path.Combine(settingsDir, Common.SETTINGS_FILE);

			//Write data to the file now.
			using (FileStream fs = File.Open(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				XmlSerializer ser = new XmlSerializer(this.settings.GetType());
				ser.Serialize(fs, this.settings);
			}

			//Now reload settings from file.
			this.settingsLoadFromFile();
			this.settingsLoadToForm();

			//Set flag.
			this.WindowSetUnsaved(false);
		}

		/// <summary>Display the loaded values..</summary>
		private void settingsLoadToForm()
		{
			//Set data sources..
			// Application Servers
			this.lstApplicationServers.ItemsSource = this.settings.Application_Servers;
			this.lstApplicationServers.Items.Refresh();

			// Email Accounts
			this.lstEmailAccounts.ItemsSource = this.settings.Email_Accounts;
			this.lstEmailAccounts.Items.Refresh();

			//Advanced - Ignore Emails
			this.settings.Ignore_Addresses.Sort();
			this.lstIgnoreEmails.ItemsSource = this.settings.Ignore_Addresses;
			this.lstIgnoreEmails.Items.Refresh();

			//Advanced - Ignore Headers
			this.settings.Ignore_Headers.Sort();
			this.lstIgnoreHeaders.ItemsSource = this.settings.Ignore_Headers;
			this.lstIgnoreHeaders.Items.Refresh();

			//Advanced - Ignore Words
			this.settings.Ignore_Words.Sort();
			this.lstIgnoreWords.ItemsSource = this.settings.Ignore_Words;
			this.lstIgnoreWords.Items.Refresh();

			//Advanced - SpamAssassin
			this.chkUseSpamAssassin.IsChecked = this.settings.UseSpamAssassin;
			this.txtSpamAss_ServerName.Text = this.settings.SpamAssassinServer;
			this.txtSpamAss_ServerPort.Text = ((this.settings.SpamAssassinPort == 0) ? "" : this.settings.SpamAssassinPort.ToString());
			this.chkSpamAss_UseCustomSpamLevel.IsChecked = this.settings.UseCustomSALevel;
			this.txtSpamAss_CustomSALevel.Text = ((this.settings.UseCustomSALevel) ? this.settings.CustomSALevel.ToString() : "");

			//Advanced
			this.chkTrace.IsChecked = this.settings.EnableTrace;
			this.txtMinutes.Text = this.settings.PollInterval.ToSafeString();
			this.chkAllowEmptyRetPath.IsChecked = this.settings.EmptyReturnPath;

			this.WindowSetUnsaved(false);
		}

		/// <summary>Given the Application System, this will either add it or update an existing one in the list.</summary>
		/// <param name="appSys">The Application System to add or update.</param>
		private void AddOrUpdateApplicationSystem(ApplicationSystem appSystem)
		{
			if (appSystem != null && appSystem.ServerID.HasValue)
			{
				//It's a real server, so we need to add it to the list, or update an existing one.
				//Find the item..
				if (appSystem.ServerID.Value > 0 && this.settings.Application_Servers.Count(aps => aps.ServerID == appSystem.ServerID) == 1)
				{
					ApplicationSystem exisSystem = this.settings.Application_Servers.Where(aps => aps.ServerID == appSystem.ServerID).SingleOrDefault();
					if (exisSystem != null)
					{
						exisSystem.ServerType = appSystem.ServerType;
						exisSystem.ServerURL = appSystem.ServerURL;
						exisSystem.UserID = appSystem.UserID;
						exisSystem.UserPassword = appSystem.UserPassword;
					}
				}
				else
				{
					//Get the next server ID number.. Get the last server, first..
					if (this.settings.Application_Servers.Count > 0)
					{
						ApplicationSystem highestSystem = this.settings.Application_Servers.OrderBy(aps => aps.ServerID).ToList()[this.settings.Application_Servers.Count - 1];
						appSystem.ServerID = highestSystem.ServerID + 1;
					}
					else
						appSystem.ServerID = 1;

					//Add it to the list.
					this.settings.Application_Servers.Add(appSystem);
				}
			}

			//Flag the window as having changes.
			this.WindowSetUnsaved(true);

			//Refresh display.
			this.lstApplicationServers.Items.Refresh();
		}

		/// <summary>Given the Email Account, this will either add it or update an existing one in the list.</summary>
		/// <param name="emailAcct">The Email Account to add or update.</param>
		private void AddOrUpdateEmailAccount(EmailAccount emailAcct)
		{
			if (emailAcct != null && emailAcct.AccountID.HasValue)
			{
				//It's a real account, so we need to add it to the list, or update an existing one.
				//Find it first..
				if (emailAcct.AccountID.Value > 0 && this.settings.Email_Accounts.Count(em => em.AccountID == emailAcct.AccountID) == 1)
				{
					EmailAccount exisAcct = this.settings.Email_Accounts.Where(aps => aps.AccountID == emailAcct.AccountID).SingleOrDefault();
					if (exisAcct != null)
					{
						exisAcct.AccountEmail = emailAcct.AccountEmail;
						exisAcct.ApplicationServerID = emailAcct.ApplicationServerID;
						exisAcct.ProductOrProjectId = emailAcct.ProductOrProjectId;
						exisAcct.RemoveFromServer = emailAcct.RemoveFromServer;
						exisAcct.Save3rdPartyAsAttachment = emailAcct.Save3rdPartyAsAttachment;
						exisAcct.SaveRawEmailAsAttachment = emailAcct.SaveRawEmailAsAttachment;
						exisAcct.ServerNameOrIP = emailAcct.ServerNameOrIP;
						exisAcct.ServerPort = emailAcct.ServerPort;
						exisAcct.ServerType_IMAP = emailAcct.ServerType_IMAP;
						exisAcct.ServerType_MAPI = emailAcct.ServerType_MAPI;
						exisAcct.UseRegexToMatch = emailAcct.UseRegexToMatch;
						exisAcct.UserLogin = emailAcct.UserLogin;
						exisAcct.UserPass = emailAcct.UserPass;
						exisAcct.UseSSL = emailAcct.UseSSL;
					}
				}
				else
				{
					//Get the next server ID number.. Get the last server, first..
					if (this.settings.Email_Accounts.Count > 0)
					{
						EmailAccount highestAcct = this.settings.Email_Accounts.OrderBy(em => em.AccountID).ToList()[this.settings.Email_Accounts.Count - 1];
						emailAcct.AccountID = highestAcct.AccountID + 1;
					}
					else
						emailAcct.AccountID = 1;

					//Add it to the list.
					this.settings.Email_Accounts.Add(emailAcct);
				}
			}

			//Flag the window as having changes.
			this.WindowSetUnsaved(true);

			//Refresh display.
			this.lstEmailAccounts.Items.Refresh();
		}
	}
}
