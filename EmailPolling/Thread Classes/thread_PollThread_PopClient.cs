using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Inflectra.POP3;
using Inflectra.KronoDesk.Service.Email.Settings;
using Inflectra.POP3.Mime;
using Inflectra.POP3.Mime.Header;
using System.Diagnostics;
using Inflectra.KronoDesk.Service.Email.Settings.KronoClient;
using System.Text.RegularExpressions;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace Inflectra.KronoDesk.Service.Email.Service
{
	/// <summary>Class that has static functions for commonly-used POP3 Client commands.</summary>
	public partial class PollThread
	{
		private static string EMAIL_SEPARATOR_HTML = "------=&gt; Please keep your reply above this line. &lt;=-";
		private static string EMAIL_SEPARATOR_TEXT = "------=> Please keep your reply above this line. <=-";

		//List of our Regexes..
		private static RegexOptions rgxOpt = RegexOptions.IgnoreCase | RegexOptions.Compiled;
		private List<Regex> seperatorText = new List<Regex> {
			new Regex(@"(\<br\>|\n)\s*" + Regex.Escape("------=" + @"(\&gt\;|>)"), rgxOpt),
			new Regex(@"From:\s*.+", rgxOpt),
			new Regex(@"\n.*On.*(\r\n)?wrote:\r\n", rgxOpt| RegexOptions.Multiline),
			new Regex(@"\s+wrote:", rgxOpt),
			new Regex(@"-+original\s+message-+\s*$", rgxOpt)
		};

		/// <summary>Creates a POP3 client and logs in the specified user.</summary>
		/// <param name="log">Event Log for logging connection issues.</param>
		/// <param name="account">The account to use when creating the POP3 client.</param>
		/// <returns>A new POP3 client, connected and logged on.</returns>
		private Pop3Client popConnect(AccountDetails account)
		{
			const string METHOD = CLASS + "Connect()";
			_eventLog.EntryLog(METHOD);

			//Create object..
			Pop3Client retClient = new Pop3Client();

			//Connect to the server.
			_eventLog.WriteTrace(METHOD, "Connecting to server.");
			retClient = new POP3.Pop3Client();
			retClient.Connect(
				account.ServerNameOrIP,
				account.ServerPort,
				account.UseSSL,
				account.SSLVersion,
				240000,
				240000,
				new RemoteCertificateValidationCallback(ValidateTestServerCertificate)
			);

			//Debug logging server's capabilities.
			if (_eventLog.TraceEnabled)
			{
				try
				{
					Dictionary<string, List<string>> caps = retClient.Capabilities();
					string msg = "Capabilities reported by server:" + Environment.NewLine;
					foreach (KeyValuePair<string, List<string>> cap in caps)
					{
						msg += cap.Key + ": ";
						foreach (string item in cap.Value)
						{
							msg += "'" + item + "', ";
						}
						msg = msg.Trim().Trim(',') + Environment.NewLine;
					}
					_eventLog.WriteTrace(METHOD, msg);
				}
				catch (Exception ex)
				{
					_eventLog.WriteMessage(METHOD, ex, "Trying to get server's capabilities.");
				}
			}

			//Now verify our username and password.
			_eventLog.WriteTrace(METHOD, "Authenticating with server.");
			retClient.Authenticate(account.UserLogin, account.UserPass, POP3.AuthenticationMethod.Auto);

			//Return.
			_eventLog.ExitLog(METHOD);
			return retClient;
		}

		/// <summary>Gets new messages from the POP client.</summary>
		/// <param name="clientPOP3">The POP3 client to use.</param>
		/// <param name="account">The account we're getting mesages for.</param>
		/// <param name="messageIds">List of message IDs we've already gotten.</param>
		/// <returns>A list of new messages.</returns>
		private List<Message> popGetNewMessages(Pop3Client clientPOP3, AccountDetails account, List<string> messageIds)
		{
			const string METHOD = CLASS + "popGetMessage()";
			_eventLog.EntryLog(METHOD);

			//The return list..
			List<Message> retList = new List<Message>();

			//Get the account's UIDs..
			_eventLog.WriteTrace(METHOD, "Reading message UIDs.");

			//Get list of all UIDs.
			int highCount = clientPOP3.GetMessageCount();
			List<string> msgUIDs = clientPOP3.GetMessageUids();

			//Loop through each UID..
			_eventLog.WriteTrace(METHOD, "# messages on server: " + highCount.ToString() + " - Starting message loop.");
			for (int i = 0; i < msgUIDs.Count; i++)
			{
				if (!messageIds.Contains(msgUIDs[i]))
				{
					try
					{
						//Get message.
						Message msg = clientPOP3.GetMessage(i + 1);

						//Log message contents..
						string logMsg = "Message #" + (i + 1).ToString() + " (" + msgUIDs[i] + ") Pulled. Subject: " + Environment.NewLine + msg.Headers.Subject + Environment.NewLine + Environment.NewLine;
						logMsg += popGetMessageDetails(msg);
						_eventLog.WriteTrace(METHOD, logMsg);

						//Add the server's UID to the message for later.
						msg.MessageUID = msgUIDs[i];
						msg.MessageIndex = i + 1;

						//Add the message to the list.
						retList.Add(msg);
					}
					catch (Exception ex)
					{
						string logMsg = "Error pulling message #" + (i + 1).ToString() + " (" + msgUIDs[i] + ").";
						_eventLog.WriteMessage(METHOD, ex, logMsg);
					}
				}
				else
				{
					_eventLog.WriteTrace(METHOD, "Message #" + (i + 1).ToString() + " (" + msgUIDs[i] + ") already processed. Skipping.");
				}
			}

			_eventLog.ExitLog(METHOD);
			return retList;
		}

		/// <summary>Gets a debugging string for the given email message. Mostly used for trace logging.</summary>
		/// <param name="msg">The message to grab information from.</param>
		/// <returns>Debugging log string.</returns>
		private string popGetMessageDetails(Message msg)
		{
			string retString = "";

			//Now process the message.
			retString += "# Attachments reported: " + msg.FindAllAttachments().Count + Environment.NewLine;
			retString += "# Text Parts: " + msg.FindAllTextVersions().Count + Environment.NewLine;
			retString += "Standard Headers:" + Environment.NewLine;
			retString += " - BCC: ";
			foreach (var bcc in msg.Headers.Bcc)
				retString += "'" + bcc.Raw + "', ";
			retString = retString.Trim().Trim(',') + Environment.NewLine;
			retString += " - CC: ";
			foreach (var cc in msg.Headers.Cc)
				retString += "'" + cc.Raw + "', ";
			retString = retString.Trim().Trim(',') + Environment.NewLine;
			retString += " - Content Description: " + msg.Headers.ContentDescription + Environment.NewLine;
			retString += " - Content Disposition: " + msg.Headers.ContentDisposition + Environment.NewLine;
			retString += " - Content ID: " + msg.Headers.ContentId + Environment.NewLine;
			retString += " - Content Transfer Encoding: " + msg.Headers.ContentTransferEncoding + Environment.NewLine;
			retString += " - Content Type: " + msg.Headers.ContentType.ToSafeString() + Environment.NewLine;
			retString += " - Date: " + msg.Headers.DateSent.ToShortDateString() + Environment.NewLine;
			retString += " - DispositionNotificationTo: ";
			foreach (var dispoTo in msg.Headers.DispositionNotificationTo)
				retString += "'" + dispoTo.Raw + "', ";
			retString = retString.Trim().Trim(',') + Environment.NewLine;
			retString += " - From: " + msg.Headers.From.ToSafeString() + Environment.NewLine;
			retString += " - Importance: " + msg.Headers.Importance.ToSafeString() + Environment.NewLine;
			retString += " - InReplyTo: ";
			foreach (var replyTo in msg.Headers.InReplyTo)
				retString += "'" + replyTo + "', ";
			retString = retString.Trim().Trim(',') + Environment.NewLine;
			retString += " - Keywords: ";
			foreach (var key in msg.Headers.Keywords)
				retString += "'" + key + "', ";
			retString = retString.Trim().Trim(',') + Environment.NewLine;
			retString += " - Message ID: " + msg.Headers.MessageId + Environment.NewLine;
			retString += " - Mime Version: " + msg.Headers.MimeVersion + Environment.NewLine;
			retString += " - Recieved: ";
			foreach (var rec in msg.Headers.Received)
				retString += "'" + rec.Raw + "', ";
			retString = retString.Trim().Trim(',') + Environment.NewLine;
			retString += " - References: ";
			foreach (var refer in msg.Headers.References)
				retString += "'" + refer + "', ";
			retString = retString.Trim().Trim(',') + Environment.NewLine;
			retString += " - Reply To: " + msg.Headers.ReplyTo.ToSafeString() + Environment.NewLine;
			retString += " - Return Path: " + msg.Headers.ReturnPath.ToSafeString() + Environment.NewLine;
			retString += " - Sender: " + msg.Headers.Sender.ToSafeString() + Environment.NewLine;
			retString += " - To: ";
			foreach (var refer in msg.Headers.References)
				retString += "'" + refer + "', ";
			retString = retString.Trim().Trim(',') + Environment.NewLine;
			retString += "Other Headers:" + Environment.NewLine;
			foreach (string s in msg.Headers.UnknownHeaders)
				retString += " - " + s + ": " + msg.Headers.UnknownHeaders[s] + Environment.NewLine;
			retString += Environment.NewLine;

			return retString;
		}

		/// <summary>Returns status whether or not the message passes all filters. True if the message is good, otherwise, false.</summary>
		/// <param name="msg">The message to check.</param>
		/// <returns>True of the message passes all fitlers. False if the message matches an ignore filter.</returns>
		private bool doesMessageClearFilters(Message msg, out string filterMsg, List<string> blockList = null)
		{
			const string METHOD = CLASS + "doesMessageClearFilters()";
			_eventLog.EntryLog(METHOD);

			bool retValue = true;
			string msgErr = "";

			//Check for locally-defined email addresses.
			foreach (string address in _settings.Ignore_Addresses)
			{
				if (string.Compare(address, msg.Headers.From.MailAddress.Address, StringComparison.OrdinalIgnoreCase) == 0)
				{
					//The strings match.
					msgErr = "Matched blocked email address '" + address + "' defined in Importer.";
					retValue = false;
					break;
				}
			}

			//Check for email addresses defined in KronoDesk.
			if (blockList != null && blockList.Count > 0)
			{
				foreach (string address in blockList)
				{
					if (string.Compare(address, msg.Headers.From.MailAddress.Address, StringComparison.OrdinalIgnoreCase) == 0)
					{
						//The strings match.
						msgErr = "Matched blocked email address '" + address + "' defined in Kronodesk.";
						retValue = false;
						break;
					}
				}
			}

			//Now check for headers..
			foreach (string header in _settings.Ignore_Headers)
			{
				if (msg.Headers.UnknownHeaders[header] != null)
				{
					//The header exists.
					msgErr += Environment.NewLine + "Matched on header '" + header + "'";
					retValue = false;
					break;
				}
			}

			//Now check the body for any matched words.
			List<MessagePart> msgBodies = msg.FindAllTextVersions().Where(tv => tv.IsAttachment == false && tv.IsMultiPart == false).ToList();
			foreach (MessagePart msgBody in msgBodies)
			{
				foreach (string word in _settings.Ignore_Words)
				{
					string body = msgBody.GetBodyAsText();
					if (body.ToLowerInvariant().Contains(word.ToLowerInvariant()))
					{
						//The header exists.
						msgErr += Environment.NewLine + "Matched message Body on keyword '" + word + "'";
						retValue = false;
						break;
					}
				}
			}

			//Check the subject for any matched words..
			foreach (string word in _settings.Ignore_Words)
			{
				if (msg.Headers.Subject.ToLowerInvariant().Contains(word.ToLowerInvariant()))
				{
					//Matched subject on word.
					msgErr += Environment.NewLine + "Matched message subject on word '" + word + "'";
					retValue = false;
					break;
				}
			}

			//Check to see if it's a postmaster message..
			if (msg.Headers.ContentType.MediaType.ToLowerInvariant() == "multipart/report")
			{
				msgErr += Environment.NewLine + "Content-Type was \"" + msg.Headers.ContentType.MediaType + "\", likely postmaster message.";
				retValue = false;
			}

			//Check to make sure there's a return path..
			if (!_settings.EmptyReturnPath)
			{
				if (msg.Headers.ReturnPath == null || !msg.Headers.ReturnPath.HasValidMailAddress)
				{
					msgErr += Environment.NewLine + "No return path in message. Likely postmaster message.";
					retValue = false;
				}
			}

			//Check SpamAssassin score..
			if (_settings.UseSpamAssassin)
			{
				List<SpamAssassinCheck.RuleResult> results = SpamAssassinCheck.GetReport(_settings.SpamAssassinServer, _settings.SpamAssassinPort, Encoding.UTF8.GetString(msg.RawMessage));
				if (_settings.UseCustomSALevel)
				{
					//Get the score.
					SpamAssassinCheck.RuleResult scoreRes = results.Where(res => res.Rule == "SPAM_LEVEL").SingleOrDefault();
					if (scoreRes != null && scoreRes.Score >= _settings.CustomSALevel)
					{
						retValue = false;
						msgErr += Environment.NewLine + "SpamAssassin score (" + scoreRes.Score.ToString() + ") matched or exceeded custom level (" + _settings.CustomSALevel.ToString() + "). [" + scoreRes.Description.Trim() + "]";
					}
				}
				else
				{
					//See if SA marked it as spam or not.
					SpamAssassinCheck.RuleResult scoreRes = results.Where(res => res.Rule == "IS_SPAM").SingleOrDefault();
					if (scoreRes != null && scoreRes.Score >= 1)
					{
						retValue = false;
						msgErr += Environment.NewLine + "SpamAssassin marked this email as spam. [" + scoreRes.Description.Trim() + "]";
					}
				}
			}

			//Save the message.
			filterMsg = msgErr.Trim();

			_eventLog.ExitLog(METHOD);
			return retValue;
		}

		/// <summary>Takes the message part and returns the body of the text that we care about.</summary>
		/// <param name="messagePart">The email message part.</param>
		/// <returns>The string of the comment we want to save.</returns>
		private string getTextFromMessage(Message msg, bool findHTML = false, bool searchForSeperator = false, List<string> seperators = null)
		{
			string retMessage = "";

			MessagePart part = null;

			if (findHTML)
			{
				part = msg.FindFirstHtmlVersion();
				if (part == null)
				{
					part = msg.FindFirstPlainTextVersion();
					retMessage = part.GetBodyAsText().ToHTML();
				}
				else
				{
					try
					{
						retMessage = part.GetBodyAsText().StripWORD().ToFixedHtml();
					}
					catch (Exception ex)
					{
						part = msg.FindFirstPlainTextVersion();
						retMessage = part.GetBodyAsText().ToHTML();

						//Log bad message action..
						_eventLog.WriteMessage("", ex, "Error retrieving HTML from message. Using plain test part instead.");
					}
					//Check for major missing data..
					if (retMessage.Length < (part.GetBodyAsText().Length * .5))
					{
						part = msg.FindFirstPlainTextVersion();
						if (part != null)
							retMessage = part.GetBodyAsText().ToHTML();
					}
				}
			}
			else
				retMessage = msg.FindFirstPlainTextVersion().GetBodyAsText();

			//Some logfic to try and best find the seperator.
			if (searchForSeperator)
			{
				int sepLoc = 0;

				//First loop through the strings we got from 
				int idx = 0;
				while (sepLoc == 0 && idx < seperators.Count)
				{
					sepLoc = retMessage.IndexOf(seperators[idx]);
					idx++;
				}

				//If we did not have any matches, do a more white-text based search.
				idx = 0;
				if (sepLoc == 0)
				{
					while (sepLoc == 0 && idx < seperatorText.Count)
					{
						if (seperatorText[idx].IsMatch(retMessage))
							sepLoc = seperatorText[idx].Match(retMessage).Index;
						idx++;
					}
				}

				//Now, let's split on the message, if we can!
				if (sepLoc > 0)
					retMessage = retMessage.Substring(sepLoc);
			}

			return retMessage;
		}

		/// <summary>Returns the best defined first, last name from the email address.</summary>
		/// <param name="address">The email address to get the name from.</param>
		/// <returns>A dictionary with keys of 'first' and 'last'.</returns>
		private Dictionary<string, string> getFullNameFromAddress(RfcMailAddress address)
		{
			Dictionary<string, string> retNames = new Dictionary<string, string>();

			if (!string.IsNullOrWhiteSpace(address.DisplayName))
			{
				//Split the words..
				if (address.DisplayName.Contains(','))
				{
					string[] names1 = address.DisplayName.Split(',');

					retNames.Add("last", names1[0].Trim());
					retNames.Add("first", names1[names1.Count() - 1].Trim());

				}
				else
				{
					string[] names1 = address.DisplayName.Split(' ');

					if (names1.Count() == 1)
					{
						retNames.Add("first", names1[0].Trim());
						retNames.Add("last", "--");
					}
					else
					{
						retNames.Add("first", names1[0].Trim());
						retNames.Add("last", names1[names1.Count() - 1].Trim());
					}
				}

			}
			else
			{
				retNames.Add("last", "--");
				retNames.Add("first", "--");
			}

			//Trim the First and Last names, incase..
			if (retNames["first"].Length > 49) retNames["first"] = retNames["first"].Substring(0, 49);
			if (retNames["last"].Length > 49) retNames["last"] = retNames["last"].Substring(0, 49);
			if (string.IsNullOrWhiteSpace(retNames["first"].Trim())) retNames["first"] = "--";
			if (string.IsNullOrWhiteSpace(retNames["last"].Trim())) retNames["last"] = "--";

			return retNames;
		}

		/// <summary>Accept any certificate given to us. It prevents self-signed certificates causing issues.</summary>
		/// <returns>Whether certificate is good or not.</returns>
		private static bool ValidateTestServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			// Accept any certificate
			return true;
		}
	}
}
