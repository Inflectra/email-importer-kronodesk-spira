using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Inflectra.POP3;
using Inflectra.KronoDesk.Service.Email.Settings;
using Inflectra.POP3.Mime;
using System.Text.RegularExpressions;
using Inflectra.KronoDesk.Service.Email.Settings.KronoClient;
using System.Security.Cryptography;
using System.Web.Security;
using System.IO;

namespace Inflectra.KronoDesk.Service.Email.Service
{
	/// <summary>The part of the class that handles communicating with a Spira server.</summary>
	public partial class PollThread
	{
		private void processKronoAccount(Pop3Client clientPOP3, AccountDetails account, ApplicationSystem appServer)
		{
			const string METHOD = CLASS + "processKronoAccount()";
			_eventLog.EntryLog(METHOD);

			//Create the application client and connect to our project and get users..
			SoapServiceClient clientAppl = (SoapServiceClient)this.CreateApplicationClient(appServer, account);

			//Get the products and settings from Kronodesk.
			List<RemoteProduct> krnProds = clientAppl.Product_Retrieve(false);

			//Get list of blocked emails..
			List<string> blockedEmails = clientAppl.System_GetBlockedEmails();
			//Get list of seperator lines..
			List<string> seperatorLines = clientAppl.System_GetEmailSeparator();

			//Get the known message IDs..
			List<string> seenUIDs = readMessageIDsForAccount(account.AccountID.Value);

			//Get new emails from the client.
			List<Message> newMsgs = popGetNewMessages(clientPOP3, account, seenUIDs);

			//Loop through each email.
			foreach (Message msg in newMsgs)
			{
				try
				{
					_eventLog.WriteTrace(METHOD, "Starting on message " + msg.MessageLogID + "...");

					//First see if the message should be skipped. (Keywords, Headers, or Email Addresses)
					if (msg.Headers != null &&
						msg.Headers.From != null &&
						!string.IsNullOrWhiteSpace(msg.Headers.From.Address) &&
						msg.Headers.From.Address.ToLowerInvariant().Trim() != account.AccountEmail.ToLowerInvariant().Trim())
					{
						string filterMsg;

						//Run it though our own filters.
						if (doesMessageClearFilters(msg, out filterMsg, blockedEmails))
						{
							long? tktId = null;

							//See if we can get a ticket ID..
							if ((!tktId.HasValue) && msg.Headers.UnknownHeaders[Common.MESSAGEHEADER_KRONO_TICKET] != null)
							{
								tktId = retrieveTicketId(msg.Headers.UnknownHeaders[Common.MESSAGEHEADER_KRONO_TICKET]);
							}

							if (!tktId.HasValue)
							{
								Regex rgx = new Regex(@"\[[a-zA-Z]{2}[\:-][0-9]+\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);
								tktId = retrieveTicketId(rgx.Match(msg.Headers.Subject).Value);
							}

							//Get user that this email is from..
							RemoteUser usrFrom = clientAppl.User_RetrieveByEmailAddress(msg.Headers.From.MailAddress.Address);

                            //See if we have any CC users
                            List<string> ccList = null;
                            if (msg.Headers.Cc != null && msg.Headers.Cc.Count > 0)
                            {
                                ccList = msg.Headers.Cc.Select(c => c.MailAddress.Address).ToList();
                            }

							//Now process the email.
							RemoteTicket newTicket = null;
							RemoteTicketNote newTktNote = null;

							if (tktId.HasValue)
							{
								try
								{
									//The ticket exists, we're going to add the text as a new comment.

									//The flag on whether we need to make it a '3rd party' note.
									bool add3rd = (usrFrom == null);

									//If at least one user exists..
									if (!add3rd)
									{
										//Create the note..
										newTktNote = new RemoteTicketNote();
										newTktNote.CreationDate = DateTime.UtcNow;
										newTktNote.AuthorId = usrFrom.UserId;
										newTktNote.TicketId = tktId.Value;
										newTktNote.Text = getTextFromMessage(msg, true, true, seperatorLines);

										try
										{
											newTktNote = clientAppl.Ticket_AddNote(newTktNote);
										}
										catch (Exception ex)
										{
											_eventLog.WriteMessage("Error adding note to message. Adding it as third-party.");
											add3rd = true;
										}

										if (!add3rd)
										{
											_eventLog.WriteMessage("Note #" + newTktNote.NoteId.ToSafeString() + " added to Ticket #" + newTktNote.TicketId.ToSafeString() + " created from message " + msg.MessageLogID + ".", System.Diagnostics.EventLogEntryType.Information);

											//If we get this far, mark the message as processed.
											try
											{
												if (account.RemoveFromServer)
													clientPOP3.DeleteMessage(msg.MessageIndex);
											}
											catch (Exception ex)
											{
												_eventLog.WriteMessage(METHOD, ex, "Trying to delete message " + msg.MessageLogID + " from server.");
											}
											//We processed the message, add it.
											seenUIDs.Add(msg.MessageUID);
										}
									}

									if (add3rd)
									{
										try
										{
											//An email was sent in from a third party.
											string strDescDetails = "Full Subject: " + msg.Headers.Subject + Environment.NewLine +
												"From Address: " + ((string.IsNullOrWhiteSpace(msg.Headers.From.Address)) ? "< empty >" : msg.Headers.From.Address) + Environment.NewLine +
												"From Name:    " + ((string.IsNullOrWhiteSpace(msg.Headers.From.DisplayName)) ? "< empty >" : msg.Headers.From.DisplayName);

											RemoteDocument rawMsg = getMsgAsAttachment(msg, tktId.Value, null, "3rd Party Email:" + Environment.NewLine + strDescDetails);
											rawMsg = clientAppl.Document_AddFile(rawMsg, msg.RawMessage);

											//Add comment.
											newTktNote = new RemoteTicketNote();
											newTktNote.CreationDate = DateTime.UtcNow;
											newTktNote.TicketId = tktId.Value;
											newTktNote.Text = "An email for this ticket was recieved from a third unknown party, and has been attached to this ticket as a file. Details:" + Environment.NewLine +
												"<pre>" + strDescDetails + Environment.NewLine +
												"Attachment:   " + rawMsg.FilenameOrUrl +
												"</pre>";
											newTktNote.Text = newTktNote.Text.Replace(Environment.NewLine, "<br />"); //Convert to HTML.

											newTktNote = clientAppl.Ticket_AddNote(newTktNote);

											_eventLog.WriteMessage("Message from 3rd party to ticket. Message " + msg.MessageLogID + " saved as Attachment #" + rawMsg.AttachmentId.ToSafeString() + " & Note #" + newTktNote.NoteId + " added.", System.Diagnostics.EventLogEntryType.Information);

											//If we get this far, mark the message as processed.
											try
											{
												if (account.RemoveFromServer)
													clientPOP3.DeleteMessage(msg.MessageIndex);
											}
											catch (Exception ex)
											{
												_eventLog.WriteMessage(METHOD, ex, "Trying to delete message " + msg.MessageLogID + " from server.");
											}

											//We processed the message, add it.
											seenUIDs.Add(msg.MessageUID);
										}
										catch (Exception ex)
										{
											_eventLog.WriteMessage(METHOD, ex, "Error adding 3rd party email to ticket from message " + msg.MessageLogID);
										}
									}
								}
								catch (Exception ex)
								{
									_eventLog.WriteMessage(METHOD, ex, "Error adding note to ticket from message " + msg.MessageLogID + ".");
									break;
								}
							}
							else
							{
								try
								{
									//Create the fake user first..
									if (usrFrom == null)
									{
										RemoteUser newUser = new RemoteUser();
										newUser.Active = true;
										newUser.Approved = false;
										newUser.EmailAddress = msg.Headers.From.MailAddress.Address;
										newUser.Login = msg.Headers.From.MailAddress.Address;
										Dictionary<string, string> names = getFullNameFromAddress(msg.Headers.From);
										newUser.FirstName = names["first"];
										newUser.LastName = names["last"];

										//Check for too-long strings..
										if (newUser.FirstName.Length > 49) newUser.FirstName = newUser.FirstName.Substring(0, 49);
										if (newUser.LastName.Length > 49) newUser.LastName = newUser.LastName.Substring(0, 49);
										if (newUser.Login.Length > 49) newUser.Login = newUser.Login.Substring(0, 49);
										if (newUser.EmailAddress.Length > 49) newUser.EmailAddress = newUser.EmailAddress.Substring(0, 49);

										usrFrom = clientAppl.User_Create(newUser, "abcdefghijkLMNOPQ1", "Registration Code", Membership.GeneratePassword(10, 0), new List<string>());
										_eventLog.WriteMessage("Temporary user '" + newUser.Login + "' created, user ID#" + usrFrom.UserId.ToSafeString(), System.Diagnostics.EventLogEntryType.Information);
									}

									//Need to create a new ticket.
									newTicket = new RemoteTicket();
									newTicket.CreationDate = DateTime.UtcNow;
									newTicket.Name = msg.Headers.Subject;
									newTicket.Description = getTextFromMessage(msg, true);
                                    newTicket.OpenerId = usrFrom.UserId;
									newTicket.ProductId = account.ProductOrProjectId.Value;

                                    //Add any CC users
                                    newTicket.CCList = ccList;

                                    //Add the source
                                    newTicket.SourceId = -2; //Email

									//Check regex other products
									if (account.UseRegexToMatch)
									{
										Regex regProd1 = new Regex(@"\[PR[\:\-\s](\d*?)\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
										if (regProd1.IsMatch(newTicket.Description) || regProd1.IsMatch(newTicket.Name))
										{
											//Someone used the PR tag in the email body, we'll use it, if possible.
											string matchNum = "";
											if (regProd1.IsMatch(newTicket.Name))
												matchNum = regProd1.Matches(newTicket.Name)[0].Groups[1].Value;
											else if (regProd1.IsMatch(newTicket.Description))
												matchNum = regProd1.Matches(newTicket.Description)[0].Groups[1].Value;
											else
											{
												_eventLog.WriteMessage("ERROR: At least one RegEx returned IsMatch, but none contained match.", System.Diagnostics.EventLogEntryType.Information);
												continue;
											}

											int prodNum = 0;
											if (int.TryParse(matchNum, out prodNum))
											{
												//We had a number, let's see if it's a valid product.
												RemoteProduct prod = krnProds.Where(prd => prd.ProductId == prodNum).SingleOrDefault();
												if (prod != null)
												{
													newTicket.ProductId = (int)prod.ProductId.Value;
													_eventLog.WriteMessage("Message " + msg.MessageLogID + " changed to Product '" + prod.Name + "' due to having [PR:xx] tag.", System.Diagnostics.EventLogEntryType.Information);
												}
											}
										}
										else
										{
											bool foundToken = false;
											foreach (RemoteProduct prod in krnProds)
											{
												if (!string.IsNullOrWhiteSpace(prod.ProductToken))
												{
													Regex regProd2 = new Regex(@"\b" + prod.ProductToken + @"\b", RegexOptions.CultureInvariant | RegexOptions.Compiled);

													if (regProd2.IsMatch(newTicket.Description) || regProd2.IsMatch(newTicket.Name))
													{
														newTicket.ProductId = (int)prod.ProductId.Value;
														foundToken = true;
														_eventLog.WriteMessage("Message " + msg.MessageLogID + " changed to Product '" + prod.Name + "' due to having Product token '" + prod.ProductToken + "'", System.Diagnostics.EventLogEntryType.Information);
														break;
													}
												}
											}

											if (!foundToken)
											{
												foreach (RemoteProduct prod in krnProds)
												{
													if (!string.IsNullOrWhiteSpace(prod.Name))
													{
														Regex regProd3 = new Regex(@"\b" + prod.Name + @"\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
														if (regProd3.IsMatch(newTicket.Description) || regProd3.IsMatch(newTicket.Name))
														{
															newTicket.ProductId = (int)prod.ProductId.Value;
															_eventLog.WriteMessage("Message " + msg.MessageLogID + " changed to Product '" + prod.Name + "' due to having Product name '" + prod.Name + "'", System.Diagnostics.EventLogEntryType.Information);
															break;
														}
													}
												}
											}
										}
									}

									//Check for string overrun..
									if (newTicket.Name.Length > 254) newTicket.Name = newTicket.Name.Substring(0, 254);
									//Check for null (or empty) Name..
									if (string.IsNullOrWhiteSpace(newTicket.Name)) newTicket.Name = "(no subject)";

									newTicket = clientAppl.Ticket_Create(newTicket);
									tktId = newTicket.TicketId;
									_eventLog.WriteMessage("Ticket #" + newTicket.TicketId.ToSafeString() + " created from message " + msg.MessageLogID + ".", System.Diagnostics.EventLogEntryType.Information);

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
										_eventLog.WriteMessage(METHOD, ex, "Trying to delete message " + msg.MessageLogID + " from server.");
									}
								}
								catch (Exception ex)
								{
									_eventLog.WriteMessage(METHOD, ex, "Creating new ticket/user for message " + msg.MessageLogID);
									continue;
								}
							}

							//Check for attachments now..
							if (tktId.HasValue)
							{
								//The list of contentID's mapped to URLs..
								Dictionary<string, string> mappedURLs = new Dictionary<string, string>();

								try
								{
									//Get existing attachments and add them to the list.
									List<RemoteDocument> tktDocs = clientAppl.Document_GetForArtifact(6, tktId.Value, true);
									Dictionary<string, RemoteDocument> savedHashes = new Dictionary<string, RemoteDocument>();
									foreach (RemoteDocument doc in tktDocs)
									{
										string hash = System.Text.ASCIIEncoding.ASCII.GetString(doc.Hash).Trim();
										if (!savedHashes.ContainsKey(hash)) savedHashes.Add(hash.Trim(), doc);
									}

									foreach (MessagePart attach in msg.FindAllAttachments().Where(aa => aa.IsMultiPart == false && aa.IsText == false))
									{
										//First get the MD5 of the attachment..
										string attHash;
										using (var md5 = new MD5CryptoServiceProvider())
											attHash = System.Text.ASCIIEncoding.ASCII.GetString(md5.ComputeHash(attach.Body)).Trim();

										//Make sure the hash dosen't already exist..
										bool doesExist = false;
										if (savedHashes.ContainsKey(attHash))
										{
											//It exists, so se tthe flag and add the URL and Id to our dictionary..
											doesExist = true;
											if (!mappedURLs.ContainsKey(attach.ContentId))
												mappedURLs.Add(attach.ContentId, savedHashes[attHash].DocumentURL);
										}

										if (!doesExist)
										{
											//Add the file..
											RemoteDocument newDoc = new RemoteDocument();
											newDoc.ArtifactId = tktId.Value;
											newDoc.ArtifactTypeId = 6;
											newDoc.AttachmentTypeId = 1;
                                            newDoc.AuthorId = usrFrom.UserId;
											newDoc.FilenameOrUrl = attach.FileName;
											newDoc.UploadDate = DateTime.UtcNow;

											//Check for string overrun and add extension if necessary.
											if (newDoc.FilenameOrUrl.Length > 250) newDoc.FilenameOrUrl = newDoc.FilenameOrUrl.Substring(0, 250);
											if (string.IsNullOrWhiteSpace(Path.GetExtension(newDoc.FilenameOrUrl)) && attach.ContentType != null)
											{
												string tempFileExtension = clientAppl.System_GetExtensionFromMimeType(attach.ContentType.MediaType);
												if (!string.IsNullOrWhiteSpace(tempFileExtension))
													newDoc.FilenameOrUrl += "." + tempFileExtension;
											}

											//Call the function and update mappings..
											newDoc = clientAppl.Document_AddFile(newDoc, attach.Body);
											savedHashes.Add(attHash, newDoc);
											if (!string.IsNullOrWhiteSpace(attach.ContentId)) mappedURLs.Add(attach.ContentId, newDoc.DocumentURL);

											//Loggit.
											_eventLog.WriteMessage("Attachment #" + newDoc.AttachmentId.ToSafeString() + " created from file '" + attach.FileName + "' for Ticket #" + tktId.ToSafeString() + " from message " + msg.MessageLogID + ".", System.Diagnostics.EventLogEntryType.Information);
										}
										else
											_eventLog.WriteMessage("Attachment file '" + attach.FileName + "' not uploaded from message " + msg.MessageLogID + ", hash already exists in Ticket #" + tktId.ToSafeString() + ".", System.Diagnostics.EventLogEntryType.Information);

									}
								}
								catch (Exception ex)
								{
									_eventLog.WriteMessage(METHOD, ex, "Saving attachment for message " + msg.MessageLogID);
									break;
								}

								//If the flag's set, get the entire message..
								if (account.SaveRawEmailAsAttachment)
								{
									try
									{
                                        RemoteDocument rawMsg = getMsgAsAttachment(msg, tktId.Value, usrFrom.UserId, null);
										rawMsg = clientAppl.Document_AddFile(rawMsg, msg.RawMessage);

										_eventLog.WriteMessage("Message " + msg.MessageLogID + " saved as Attachment #" + rawMsg.AttachmentId.ToSafeString() + ".", System.Diagnostics.EventLogEntryType.Information);
									}
									catch (Exception ex)
									{
										_eventLog.WriteMessage(METHOD, ex, "Saving message " + msg.MessageLogID + " as attachment:");
										break;
									}
								}

								//Now loop through each saved attachment, and update URLs..
								bool needsTktUpdating = false;
								bool needsNoteUpdating = false;
								foreach (KeyValuePair<string, string> kvpURL in mappedURLs)
								{
									//See if we need to updat ethe note or the ticket..
									if (newTicket == null && newTktNote != null)
									{
										if (newTktNote.Text.Contains(kvpURL.Key))
										{
											newTktNote.Text = newTktNote.Text.Replace("cid:" + kvpURL.Key, kvpURL.Value);
											needsNoteUpdating = true;
										}
									}
									else if (newTicket != null && newTktNote == null)
									{
										if (newTicket.Description.Contains(kvpURL.Key))
										{
											newTicket.Description = newTicket.Description.Replace("cid:" + kvpURL.Key, kvpURL.Value);
											needsTktUpdating = true;
										}
									}
									else
									{
										//Log warning message here.
										_eventLog.WriteMessage("Both newTicket and newTktNote were null or not null while processing message " + msg.MessageLogID + ".", System.Diagnostics.EventLogEntryType.Warning);
									}
								}
								if (needsTktUpdating)
								{
									try
									{
										//The date is set to 4/26/1976 so that the KronoDesk code knows not to record history changes and send out another notification.
										//All we're doing is converting the description URLs for the newly-uploaded attachments.
										_eventLog.WriteTrace(METHOD, "Updating ticket description with attachment URLs for message " + msg.MessageLogID + ".");
										newTicket.LastUpdateDate = new DateTime(1976, 4, 26);
										clientAppl.Ticket_Update(newTicket);
									}
									catch (Exception ex)
									{
										_eventLog.WriteMessage(METHOD, ex, "Trying to update ticket description for message " + msg.MessageLogID + ".");
									}
								}
								else if (needsNoteUpdating)
								{
									try
									{
										_eventLog.WriteTrace(METHOD, "Updating note text with attachment URLs for message " + msg.MessageLogID + ".");
										clientAppl.Ticket_UpdateNote(newTktNote);
									}
									catch (Exception ex)
									{
										_eventLog.WriteMessage(METHOD, ex, "Trying to update note text for message " + msg.MessageLogID + ".");
									}
								}
							}
						}
						else
						{
							//Log it..
							_eventLog.WriteMessage("Message " + msg.MessageLogID + " on the server did not pass filters:" + Environment.NewLine + "Subject: " + msg.Headers.Subject + Environment.NewLine + "Reasons: " + Environment.NewLine + filterMsg, System.Diagnostics.EventLogEntryType.Warning);
						}
					}
					else
					{
						//Log it..
						_eventLog.WriteMessage("Message " + msg.MessageLogID + " had no From address or was from the import account --" + Environment.NewLine + "Subject: " + msg.Headers.Subject + Environment.NewLine + "Msg UID: " + msg.Headers.MessageId + Environment.NewLine + "For reason: From Email address same as importing account, or was null.", System.Diagnostics.EventLogEntryType.Warning);
					}
				}
				catch (Exception ex)
				{
					_eventLog.WriteMessage(METHOD, ex, "While importing message " + msg.MessageLogID);
				}
			}

			//Save the seen message IDs first, in case there's a problem disconnecting.
			saveMessageIDsForAccount(account.AccountID.Value, seenUIDs);

			//Disconnect client and POP3..
			try
			{
				clientPOP3.Disconnect();
				clientAppl.Connection_Disconnect();
			}
			catch (Exception ex)
			{
				_eventLog.WriteMessage("Trying to disconnect from server.", ex);
			}
		}

		/// <summary>Takes an input string and tries to get the artifact type ID and the artifact ID.</summary>
		/// <param name="input">The input string.</param>
		/// <param name="artType">The ID of the artifact type.</param>
		/// <param name="artId">The ID of the artifact.</param>
		/// <remarks>Returns -1 and -1 if there is not both an artAType and artId.</remarks>
		private long? retrieveTicketId(string input)
		{
			long? retId = null;

			//Strip spaces and brackets, if any.
			input = input.Trim(new char[] { ' ', '[', ']', '<', '>', '\t', '\n', '\r' });
			//Get the parts..
			string[] split = input.Split(new char[] { '-', ':' });
			if (split.Count() == 2)
			{
				//The first part should be the token, fixed at 'TK' for now.
				if (split[0].ToUpperInvariant().Trim() == "TK")
				{
					//Now get the number..
					long artId = -1;
					if (long.TryParse(split[1].Trim(), out artId))
					{
						retId = artId;
					}
				}
			}

			return retId;
		}

		/// <summary>Converts the email message into an attachment.</summary>
		/// <param name="msg">The message.</param>
		/// <param name="ticketId">The ticket ID for the message.</param>
		/// <param name="authorId">The creator ID of the attachment.</param>
		/// <param name="msgDescription">Optional description for the attachment.</param>
		/// <returns>A remotedocument.</returns>
		private RemoteDocument getMsgAsAttachment(Message msg, long ticketId, long? authorId, string msgDescription)
		{
			//Add the file..
			RemoteDocument retDoc = new RemoteDocument();
			retDoc.ArtifactId = ticketId;
			retDoc.ArtifactTypeId = 6;
			retDoc.AttachmentTypeId = 1;
			retDoc.AuthorId = authorId;
			retDoc.FilenameOrUrl = "Message_" + msg.MessageUID + ".eml";
			retDoc.UploadDate = DateTime.UtcNow;
			retDoc.Description = msgDescription;

			return retDoc;
		}
	}
}
