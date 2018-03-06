using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Inflectra.POP3;
using Inflectra.KronoDesk.Service.Email.Settings;
using Inflectra.KronoDesk.Service.Email.Settings.SpiraClient;
using Inflectra.POP3.Mime;
using System.Text.RegularExpressions;

namespace Inflectra.KronoDesk.Service.Email.Service
{
	/// <summary>The part of the class that handles communicating with a Spira server.</summary>
	public partial class PollThread
	{
		public static Regex rgxArtifactToken = new Regex(@"\[(?<type>[A-Z]{2})[\:\-\s](?<id>[0-9]+)\]", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

		private void processSpiraAccount(Pop3Client clientPOP3, EmailAccount account, ApplicationSystem appServer)
		{
			const string METHOD = CLASS + "processSpiraAccount()";
			this._eventLog.EntryLog(METHOD);

			//Create the application client and connect to our project and get users..
			ImportExportClient clientAppl = (ImportExportClient)this.createApplicaitonClient(appServer, account);

			//Get users in the project..
			List<RemoteProjectUser> spiraUsers = clientAppl.Project_RetrieveUserMembership();

			//Get the known message IDs..
			List<string> seenUIDs = this.readMessageIDsForAccount(account.AccountID.Value);

			//Get new emails from the client.
			List<Message> newMsgs = this.popGetNewMessages(clientPOP3, account, seenUIDs);

			//Get all projects..
			List<RemoteProject> spiraProjs = clientAppl.Project_Retrieve();

			//Loop through each email.
			foreach (Message msg in newMsgs)
			{
				this._eventLog.WriteTrace(METHOD, "Starting on message " + msg.MessageLogID + "...");

				//Make sure we have a from address, otherwise skip (Delivery Returned messages have no FROM address)
				//First see if the message should be skipped. (Keywords, Headers, or Email Addresses)
				if (msg.Headers != null &&
					msg.Headers.From != null &&
					!String.IsNullOrWhiteSpace(msg.Headers.From.Address) &&
					msg.Headers.From.Address.ToLowerInvariant().Trim() != account.AccountEmail.ToLowerInvariant().Trim())
				{
					string filterMsg;
					if (this.doesMessageClearFilters(msg, out filterMsg))
					{
						//First see if there's a header we can get the artifact ID from..
						ArtifactTypeEnum artType = ArtifactTypeEnum.None;
						int artId = -1;
						if (msg.Headers.UnknownHeaders[Common.MESSAGEHEADER_SPIRA_ARTIFACT] != null && rgxArtifactToken.IsMatch(msg.Headers.UnknownHeaders[Common.MESSAGEHEADER_SPIRA_ARTIFACT]))
						{
							//Get the art type and the id..
							Match matches = rgxArtifactToken.Match(msg.Headers.UnknownHeaders[Common.MESSAGEHEADER_SPIRA_ARTIFACT]);
							this.retrieveArtTypeAndId(matches.Groups["type"].Value, matches.Groups["id"].Value, out artType, out artId);
						}

						if (artId == -1 || artType == ArtifactTypeEnum.None)
						{
							if (rgxArtifactToken.IsMatch(msg.Headers.Subject))
							{
								//Get the art type and the id..
								Match matches = rgxArtifactToken.Match(msg.Headers.Subject);
								this.retrieveArtTypeAndId(matches.Groups["type"].Value, matches.Groups["id"].Value, out artType, out artId);
							}
						}

						//Change projects, if necessary, and if we're able to..
						try
						{
							int projNum = clientAppl.System_GetProjectIdForArtifact((int)artType, artId);
							if (projNum != 0)
							{
								bool succNewProject = clientAppl.Connection_ConnectToProject(projNum);

								if (!succNewProject)
								{
									//Couldn't connect to the project this item belongs to. Throw an error.
									this._eventLog.WriteMessage("Message " + msg.MessageLogID + " contains information for a project [PR:" + projNum.ToString() + "] that the client could not connect to. Skipping.", System.Diagnostics.EventLogEntryType.Information);
									artType = ArtifactTypeEnum.Skip;
									artId = 0;
								}
							}
						}
						catch (Exception ex)
						{
							this._eventLog.WriteMessage(METHOD, ex, "Message " + msg.MessageLogID + " - while trying to retrieve project number from artifact. Skipping.");
						}

						//If we have a match, find the user..
						if (artId > 0 && artType != ArtifactTypeEnum.None)
						{
							//Detect if more than one user is found..
							int userCnt = spiraUsers.Where(su => su.EmailAddress.ToLowerInvariant() == msg.Headers.From.MailAddress.Address.ToLowerInvariant()).Count();
							if (userCnt == 1)
							{
								RemoteProjectUser selUser = spiraUsers.Where(su => su.EmailAddress.ToLowerInvariant() == msg.Headers.From.MailAddress.Address.ToLowerInvariant()).Single();

								//See if the item exists in the server..
								RemoteArtifact remArt = null;
								try
								{
									switch (artType)
									{
										case ArtifactTypeEnum.Requirement:
											remArt = clientAppl.Requirement_RetrieveById(artId);
											break;

										case ArtifactTypeEnum.Test_Case:
											remArt = clientAppl.TestCase_RetrieveById(artId);
											break;

										case ArtifactTypeEnum.Incident:
											remArt = clientAppl.Incident_RetrieveById(artId);
											break;

										case ArtifactTypeEnum.Release:
											remArt = clientAppl.Release_RetrieveById(artId);
											break;

										case ArtifactTypeEnum.Task:
											remArt = clientAppl.Task_RetrieveById(artId);
											break;

										case ArtifactTypeEnum.Test_Set:
											remArt = clientAppl.TestSet_RetrieveById(artId);
											break;
									}

									if (remArt == null)
										throw new Exception("Artifact did not exist: " + artType.ToString() + " #" + artId.ToString());
								}
								catch (Exception ex)
								{
									this._eventLog.WriteMessage(METHOD, ex, "For message " + msg.MessageLogID + ", referenced artifact did not exist.");
									continue;
								}

								try
								{
									//The artifact exists, let's add a comment..
									RemoteComment comment = new RemoteComment();
									comment.ArtifactId = artId;
									comment.CreationDate = DateTime.UtcNow;
									comment.UserId = selUser.UserId;
									comment.Text = this.getTextFromMessage(msg, true, true);

									switch (artType)
									{
										case ArtifactTypeEnum.Requirement:
											clientAppl.Requirement_CreateComment(comment);
											break;

										case ArtifactTypeEnum.Test_Case:
											clientAppl.TestCase_CreateComment(comment);
											break;

										case ArtifactTypeEnum.Incident:
											clientAppl.Incident_AddComments(new List<RemoteComment>() { comment });
											break;

										case ArtifactTypeEnum.Release:
											clientAppl.Release_CreateComment(comment);
											break;

										case ArtifactTypeEnum.Task:
											clientAppl.Task_CreateComment(comment);
											break;

										case ArtifactTypeEnum.Test_Set:
											clientAppl.TestSet_CreateComment(comment);
											break;
									}
								}
								catch (Exception ex)
								{
									this._eventLog.WriteMessage(METHOD, ex, "While trying to save message '" + msg.MessageLogID + "' as a new comment.");
									continue;
								}

								//If we get this far, mark the message as processed.
								try
								{
									//Add it to our list.. 
									seenUIDs.Add(msg.MessageUID);

									if (account.RemoveFromServer)
										clientPOP3.DeleteMessage(msg.MessageIndex);
								}
								catch (Exception ex)
								{
									this._eventLog.WriteMessage(METHOD, ex, "Trying to delete message " + msg.MessageLogID + " from server.");
								}
							}
							else
							{
								string msgLog = "";
								if (userCnt == 0)
									msgLog = "Message " + msg.MessageLogID + " was sent from a user not a mamber of the specified project. Not importing unknown users.";
								else if (userCnt > 1)
									msgLog = "Message " + msg.MessageLogID + " was sent from a user that did not have a unique email address. Cannot import to avoid selecting the wrong user.";

								this._eventLog.WriteMessage(msgLog, System.Diagnostics.EventLogEntryType.Information);
							}
						}
						else
						{
							if (artType != ArtifactTypeEnum.Skip)
							{
								int userCnt = spiraUsers.Where(su => su.EmailAddress.ToLowerInvariant() == msg.Headers.From.MailAddress.Address.ToLowerInvariant()).Count();
								if (userCnt == 1)
								{
									RemoteProjectUser selUser = spiraUsers.Where(su => su.EmailAddress.ToLowerInvariant() == msg.Headers.From.MailAddress.Address.ToLowerInvariant()).Single();

									//Create a new Incident..
									RemoteIncident newIncident = new RemoteIncident();
									newIncident.CreationDate = DateTime.Now;
									newIncident.Description = this.getTextFromMessage(msg, true, false);
									newIncident.Name = msg.Headers.Subject;
									newIncident.OpenerId = selUser.UserId;

									//Check regex other products
									if (account.UseRegexToMatch)
									{
										Regex regProj1 = new Regex(@"\[PR[\:\-\s](\d*?)\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
										if (regProj1.IsMatch(newIncident.Description) || regProj1.IsMatch(newIncident.Name))
										{
											//Someone used the PR tag in the email body, we'll use it, if possible.
											string matchNum = "";
											if (regProj1.IsMatch(newIncident.Name))
												matchNum = regProj1.Matches(newIncident.Name)[0].Groups[1].Value;
											else if (regProj1.IsMatch(newIncident.Description))
												matchNum = regProj1.Matches(newIncident.Description)[0].Groups[1].Value;
											else
											{
												this._eventLog.WriteMessage("ERROR: At least one RegEx returned IsMatch, but none contained match.", System.Diagnostics.EventLogEntryType.Information);
												continue;
											}

											int projNum = 0;
											if (int.TryParse(matchNum, out projNum))
											{
												//We had a number, let's see if it's a valid product.
												RemoteProject proj = spiraProjs.FirstOrDefault(prd => prd.ProjectId == projNum);
												if (proj != null)
												{
													//Connect to project..
													if (clientAppl.Connection_ConnectToProject(proj.ProjectId.Value))
													{
														newIncident.ProjectId = proj.ProjectId.Value;
														this._eventLog.WriteMessage("Message " + msg.MessageLogID + " changed to Project '" + proj.Name + "' due to having [PR:xx] tag.", System.Diagnostics.EventLogEntryType.Information);
													}
													else
													{
														this._eventLog.WriteMessage("Message " + msg.MessageLogID + " contained project token for project '" + proj.Name + "', but email import has no access to that project. Using default.", System.Diagnostics.EventLogEntryType.Information);
													}
												}
												else
												{
													this._eventLog.WriteMessage("Message '" + msg.MessageLogID + "' contained token for project " + projNum.ToString() + " but project was inaccessible. Using default.", System.Diagnostics.EventLogEntryType.Information);
												}
											}
										}
										else
										{
											foreach (RemoteProject prod in spiraProjs)
											{
												Regex regProd3 = new Regex(@"\b" + prod.Name + @"\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
												if (regProd3.IsMatch(newIncident.Description) || regProd3.IsMatch(newIncident.Name))
												{
													newIncident.ProjectId = prod.ProjectId.Value;
													this._eventLog.WriteMessage("Message " + msg.MessageLogID + " changed to Product '" + prod.Name + "' due to having Product name '" + prod.Name + "'", System.Diagnostics.EventLogEntryType.Information);
													break;
												}
											}
										}
									}
									//Now save the Incident..
									try
									{
										clientAppl.Incident_Create(newIncident);

										//If we get this far, mark the message as processed.
										try
										{
											//Add it to our list.. 
											seenUIDs.Add(msg.MessageUID);

											if (account.RemoveFromServer)
												clientPOP3.DeleteMessage(msg.MessageIndex);
										}
										catch (Exception ex)
										{
											this._eventLog.WriteMessage(METHOD, ex, "Trying to delete message " + msg.MessageLogID + " from server.");
										}
									}
									catch (Exception ex)
									{
										this._eventLog.WriteMessage(METHOD, ex, "While trying to save message '" + msg.MessageLogID + "' as an incident.");
									}
								}
								else
								{
									string msgLog = "";
									if (userCnt == 0)
										msgLog = "Message " + msg.MessageLogID + " was sent from a user not a mamber of the specified project. Not importing unknown users.";
									else if (userCnt > 1)
										msgLog = "Message " + msg.MessageLogID + " was sent from a user that did not have a unique email address. Cannot import to avoid selecting the wrong user.";

									this._eventLog.WriteMessage(msgLog, System.Diagnostics.EventLogEntryType.Information);
								}
							}
							else
							{ }
						}
					}
					else
					{
						//Log it..
						this._eventLog.WriteMessage("Message " + msg.MessageLogID + " on the server did not pass filters:" + Environment.NewLine + "Subject: " + msg.Headers.Subject + Environment.NewLine + "Reasons: " + Environment.NewLine + filterMsg, System.Diagnostics.EventLogEntryType.Warning);
					}
				}
				else
				{
					//Log it..
					this._eventLog.WriteMessage("Message " + msg.MessageLogID + " had no From address or was from the import account --" + Environment.NewLine + "Subject: " + msg.Headers.Subject + Environment.NewLine + "Msg UID: " + msg.Headers.MessageId + Environment.NewLine + "For reason: From Email address same as importing account, or was null.", System.Diagnostics.EventLogEntryType.Warning);
				}
			}
			//Save the seen message IDs..
			this.saveMessageIDsForAccount(account.AccountID.Value, seenUIDs);

			//Disconnect client and POP3..
			try
			{
				clientPOP3.Disconnect();
				clientAppl.Connection_Disconnect();
			}
			catch (Exception ex)
			{
				this._eventLog.WriteMessage(METHOD, ex, "Trying to close connections.");
			}
		}

		/// <summary>Takes an input string and tries to get the artifact type ID and the artifact ID.</summary>
		/// <param name="input">The input string.</param>
		/// <param name="artType">The ID of the artifact type.</param>
		/// <param name="artId">The ID of the artifact.</param>
		/// <remarks>Returns -1 and -1 if there is not both an artAType and artId.</remarks>
		private void retrieveArtTypeAndId(string strType, string strId, out ArtifactTypeEnum artType, out int artId)
		{
			artType = ArtifactTypeEnum.None;
			artId = -1;

			//The first part should be the token.
			switch (strType.ToUpperInvariant().Trim())
			{
				case "RQ": //Requirement
					artType = ArtifactTypeEnum.Requirement;
					break;

				case "TC": //Test Case
					artType = ArtifactTypeEnum.Test_Case;
					break;

				case "IN": //Incident
					artType = ArtifactTypeEnum.Incident;
					break;

				case "RL":
					artType = ArtifactTypeEnum.Release;
					break;

				case "TK":
					artType = ArtifactTypeEnum.Task;
					break;

				case "TX":
					artType = ArtifactTypeEnum.Test_Set;
					break;

				case "PR": // Project
					artType = ArtifactTypeEnum.Project;
					break;

				case "TR": // Test Run
				case "TS": // Test Step
				case "AH": // Automation Host
				case "AE": // Automation Engine
				default:
					artType = ArtifactTypeEnum.None;
					return;
			}

			//Now ge tthe number..
			if (!int.TryParse(strId, out artId))
			{
				artId = -1;
				artType = ArtifactTypeEnum.None;
			}
			return;
		}

		private enum ArtifactTypeEnum : int
		{
			/// <summary>Used internally to mark this message as being skipped.</summary>
			Skip = -2,
			/// <summary>Not used in this application.</summary>
			Project = -1,
			/// <summary>Used to indicate there's no artifact defined in the email.</summary>
			None = 0,
			/// <summary>Requirement Type.</summary>
			Requirement = 1,
			/// <summary>Test Case Type.</summary>
			Test_Case = 2,
			/// <summary>Incident Type.</summary>
			Incident = 3,
			/// <summary>Release Type.</summary>
			Release = 4,
			/// <summary>Test Run Type.</summary>
			Test_Run = 5,
			/// <summary>Task Type.</summary>
			Task = 6,
			/// <summary>Test Step Type. Not used in this application.</summary>
			Test_Step = 7,
			/// <summary>Test Step Type.</summary>
			Test_Set = 8,
			/// <summary>Automation Host Type. Not used in this application.</summary>
			Automation_Host = 9,
			/// <summary>Automation Engine Type. Not used in this application.</summary>
			Automation_Engine = 10
		}
	}
}
