using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace Inflectra.KronoDesk.Service.Email.Settings
{
	[Serializable()]
	public class SettingsStorage_100
	{
		public const string CONFIG_VERSION = "1.0.0";

		/// <summary>Constructior.</summary>
		public SettingsStorage_100()
		{
			//Initialize defaults.
			Email_Accounts = new List<AccountDetails>();
			Application_Servers = new List<ApplicationSystem>();
			PollInterval = Common.SETTINGS_DEFAULT_POLLINTERVAL;
			EnableTrace = Common.SETTINGS_DEFAULT_TRACE;
			Settings_Version = CONFIG_VERSION;
			Ignore_Addresses = new List<string>();
			Ignore_Headers = new List<string>();
			Ignore_Words = new List<string>();
		}

		/// <summary>Whether the application has been initialized or not.</summary>
		public bool System_Initialized
		{ get; set; }

		/// <summary>Whether or not to log debug messages.</summary>
		public bool EnableTrace
		{ get; set; }

		/// <summary>The list of configured email accounts.</summary>
		public List<AccountDetails> Email_Accounts
		{ get; set; }

		/// <summary>The list of server definiitions.</summary>
		public List<ApplicationSystem> Application_Servers
		{ get; set; }

		/// <summary>Returns the highest account #.</summary>
		[XmlIgnore()]
		public int HighestAccountId
		{
			get
			{
				//Figure the highest number. 
				int retInt = 0;

				if (Email_Accounts != null && Email_Accounts.Count > 1)
				{
					AccountDetails acct = Email_Accounts.OrderByDescending(em => em.AccountID).First();
					retInt = acct.AccountID.Value;
				}

				return retInt;
			}
		}

		/// <summary>Returns the highest application server #.</summary>
		[XmlIgnore()]
		public int HighestServerId
		{
			get
			{
				//Figure the highest number. 
				int retInt = 0;

				if (Application_Servers != null && Application_Servers.Count > 1)
				{
					ApplicationSystem app = Application_Servers.OrderByDescending(em => em.ServerID).First();
					retInt = app.ServerID.Value;
				}

				return retInt;
			}
		}

		/// <summary>How many seconds between polls. (From end of last poll to start of next poll.)</summary>
		public int PollInterval
		{ get; set; }

		/// <summary>Whether or not to import emails with empty 'Return-Path' headers.</summary>
		public bool EmptyReturnPath
		{ get; set; }

		/// <summary>If enabled, will use the SpamAssassin server to check emails.</summary>
		public bool UseSpamAssassin
		{ get; set; }

		/// <summary>The SpamAssassin Server name or IP.</summary>
		public string SpamAssassinServer
		{ get; set; }

		/// <summary>The SpamAssassin port to connect to.</summary>
		public int SpamAssassinPort
		{ get; set; }

		/// <summary>Whether to use a custom spam level (true), or default to SA's intrepertation.</summary>
		public bool UseCustomSALevel
		{ get; set; }

		/// <summary>The custom level, if we're using it.</summary>
		public double? CustomSALevel
		{ get; set; }

		#region Metadata Information

		public string Settings_Version
		{ get; set; }

		#endregion

		#region Addresses, Headers, Words to Block
		/// <summary>A list of email addresses that will cause emails to not be imported.</summary>
		public List<string> Ignore_Addresses
		{ get; set; }

		/// <summary>A list of email headers that will cause emails to not be imported, regardless of header value.</summary>
		public List<string> Ignore_Headers
		{ get; set; }

		/// <summary>A list of workds that, if contained within the body, will cause emails to not be imported, regardless of header value.</summary>
		public List<string> Ignore_Words
		{ get; set; }

		#endregion
	}

	/// <summary>Class containing details on an email account.</summary>
	public class AccountDetails
	{
		/// <summary>Initializes the class.</summary>
		public AccountDetails()
		{ }

		/// <summary>The unique Account ID.</summary>
		public int? AccountID
		{ get; set; }

		#region Server Settings
		/// <summary>The server's name or IP address.</summary>
		public string ServerNameOrIP
		{ get; set; }

		/// <summary>The port to connect to the server on.</summary>
		public int ServerPort
		{ get; set; }

		/// <summary>Whether to use SSL or not.</summary>
		public bool UseSSL
		{ get; set; }

        public string SSLVersion
        { get; set; }

		#endregion

		#region Account Settings
		/// <summary>The user ID of the account.</summary>
		public string UserLogin
		{ get; set; }

		/// <summary>The user account's password.</summary>
		public string UserPass
		{ get; set; }

		/// <summary>Whether to remove downloaded messages or not.</summary>
		public bool RemoveFromServer
		{ get; set; }

		/// <summary>The email address associated with the account.</summary>
		public string AccountEmail
		{ get; set; }
		#endregion

		/// <summary>The ID of the Project (SpiraTeam) or Project (KronoDesk)</summary>
		public int? ProductOrProjectId
		{ get; set; }

		/// <summary>The ID of the defined Server</summary>
		public int? ApplicationServerID
		{ get; set; }

		/// <summary>Whether to search the message for any mention of a Project (SpiraTeam) or Product (KronoDesk)</summary>
		public bool UseRegexToMatch
		{ get; set; }

		/// <summary>If true, the entire raw content of the email message will be saved as an attachment to the ticket.</summary>
		public bool SaveRawEmailAsAttachment
		{ get; set; }

		/// <summary>If set to yes, any emails from 3rd parties will be attached. Otherwise, they will be ignored.</summary>
		public bool Save3rdPartyAsAttachment
		{ get; set; }

		/// <summary>If set, the email server is connected via IMAP connection. If unset, it is POP3 or MAPI.</summary>
		public bool ServerType_IMAP
		{ get; set; }

		/// <summary>If set, the email server is connected via MAPI connection. If unset, it is POP3 or IMAP.</summary>
		public bool ServerType_MAPI
		{ get; set; }
	}

	/// <summary>Class containing details on the Server information.</summary>
	public class ApplicationSystem
	{
		private static string IMG_SPIRA = "/InflectraEmailIntegration;component/Images/app_spira.png";
		private static string IMG_KRONO = "/InflectraEmailIntegration;component/Images/app_krono.png";

		/// <summary>The ID of this server definition.</summary>
		public int? ServerID
		{ get; set; }

		/// <summary>The server type. 1: Spirateam; 2: Kronodesk</summary>
		public ApplServerTypeEnum ServerType
		{ get; set; }

		/// <summary>The base URL of the server.</summary>
		public string ServerURL
		{ get; set; }

		/// <summary>The useriD to log in as.</summary>
		public string UserID
		{ get; set; }

		/// <summary>The password of the server.</summary>
		public string UserPassword
		{ get; set; }

		/// <summary>For the display icon for the server.</summary>
		[XmlIgnore()]
		public string ServerIconResource
		{
			get
			{
				if (ServerType == ApplServerTypeEnum.Spira)
					return IMG_SPIRA;
				else if (ServerType == ApplServerTypeEnum.Krono)
					return IMG_KRONO;
				else
					return "";
			}
		}

		/// <summary>Gets the full API URL for the system.</summary>
		[XmlIgnore()]
		public string ServerAPIUrl
		{
			get
			{
				string APIreference = "";
				if (ServerType == ApplServerTypeEnum.Spira)
					APIreference = ClientFactory.SPIRA_API;
				else if (ServerType == ApplServerTypeEnum.Krono)
					APIreference = ClientFactory.KRONO_API;

				//Remove trailing slash, if necessary.
				if (ServerURL.EndsWith("/")) ServerURL = ServerURL.Trim('/');

				return ServerURL + APIreference;
			}
		}

		/// <summary>Gets a full name of the Application Server type.</summary>
		[XmlIgnore()]
		public string ServerTypeString
		{
			get
			{
				if (ServerType == ApplServerTypeEnum.Krono)
					return "KronoDesk";
				else if (ServerType == ApplServerTypeEnum.Spira)
					return "SpiraTeam";
				else
					return "";
			}
		}

		/// <summary>The enumeration for the server type.</summary>
		public enum ApplServerTypeEnum : int
		{
			Spira = 1,
			Krono = 2
		}
	}
}
