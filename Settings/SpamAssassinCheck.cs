using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;

public class SpamAssassinCheck
{
	/// <summary>Used to identify a rule report line.</summary>
	private static Regex REGEX_RULELINE = new Regex(@"^\s*?(?<score>\-?\d\.\d)\s+?(?<rulename>\w*)\s+?(?<rule_desc>.*)$",
											RegexOptions.Compiled |
											RegexOptions.CultureInvariant |
											RegexOptions.ExplicitCapture |
											RegexOptions.IgnoreCase |
											RegexOptions.Multiline |
											RegexOptions.Compiled);
	private static Regex REGEX_SPAMLINE = new Regex(@"^Spam: (?<is_spam>(True|False))\s*?;\s*?(?<spam_level>[\s\-]\d*\.\d*)\s*?\/\s*?(?<spam_thresh>[\s\-]\d*\.\d*)$",
											RegexOptions.Compiled |
											RegexOptions.CultureInvariant |
											RegexOptions.ExplicitCapture |
											RegexOptions.IgnoreCase |
											RegexOptions.Compiled);

	#region Helper Class
	/// <summary>A result for the check.</summary>
	public class RuleResult
	{
		#region Public Properties
		/// <summary>The rule's base score.</summary>
		public double Score = 0;
		/// <summary>The rule(s) that matche.</summary>
		public string Rule = "";
		/// <summary>Descripton of the rule.</summary>
		public string Description = "";
		#endregion //Public Properties

		#region Constructors
		/// <summary>Constructor.</summary>
		public RuleResult()
		{ }

		/// <summary>Constructor.</summary>
		/// <param name="line">The line to decode the rule from.</param>
		public RuleResult(string line)
		{
			Score = double.Parse(line.Substring(0, line.IndexOf(" ")).Trim());
			line = line.Substring(line.IndexOf(" ") + 1);
			Rule = line.Substring(0, 23).Trim();
			Description = line.Substring(23).Trim();
		}

		/// <summary>Creates a new instance of the class, with the specified information in the properties.</summary>
		/// <param name="score">The score of the rule.</param>
		/// <param name="rule_name">The rule's name.</param>
		/// <param name="rule_description">Description of the rule.</param>
		public RuleResult(double score, string rule_name, string rule_description)
		{
			this.Score = score;
			this.Rule = rule_name;
			this.Description = rule_description;
		}
		#endregion //Constructors
	}
	#endregion //Helper Class

	/// <summary>Sends a message through to SpamAssasin and parses the result.</summary>
	/// <param name="serverIP">The SpamAssassin's server name or IP.</param>
	/// <param name="message">The full, raw EMAIL message.</param>
	/// <param name="serverPort">The SpamAssassin's port to connect to.</param>
	/// <returns>A list of rules that match the message.</returns>
	public static List<RuleResult> GetReport(string serverIP, int serverPort, string message)
	{
		string command = "REPORT";

		StringBuilder sb = new StringBuilder();
		sb.AppendFormat("{0} SPAMC/1.2\r\n", command);
		sb.AppendFormat("Content-Length: {0}\r\n\r\n", message.Length);
		sb.Append(message);

		byte[] messageBuffer = Encoding.ASCII.GetBytes(sb.ToString());

		using (Socket spamAssassinSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
		{
			spamAssassinSocket.Connect(serverIP, serverPort);
			spamAssassinSocket.Send(messageBuffer);
			spamAssassinSocket.Shutdown(SocketShutdown.Send);

			int received;
			string receivedMessage = string.Empty;
			do
			{
				byte[] receiveBuffer = new byte[1024];
				received = spamAssassinSocket.Receive(receiveBuffer);
				receivedMessage += Encoding.ASCII.GetString(receiveBuffer, 0, received);
			}
			while (received > 0);

			spamAssassinSocket.Shutdown(SocketShutdown.Both);

			return ParseResponse(receivedMessage);
		}

	}

	/// <summary>Parses the responce from SpamAssassin.</summary>
	/// <param name="receivedMessage">The return text from SpamAssassin.</param>
	/// <returns>Gives a list of Rule Matches.</returns>
	private static List<RuleResult> ParseResponse(string receivedMessage)
	{
		//merge line endings
		receivedMessage = receivedMessage.Replace("\r\n", "\n");
		receivedMessage = receivedMessage.Replace("\r", "\n");
		string[] lines = receivedMessage.Split('\n');

		List<RuleResult> results = new List<RuleResult>();
		bool inReport = false;
		bool foundSpam = false;
		for (int i = 0; i < lines.Length; i++)
		{
			string line = lines[i].Trim();
			if (!string.IsNullOrWhiteSpace(line))
			{
				//First see if it matches our SPAM line..
				if (REGEX_SPAMLINE.IsMatch(line) && !foundSpam)
				{
					//Set flag.
					foundSpam = true;

					//Parse line.
					MatchCollection matches = REGEX_SPAMLINE.Matches(line);

					//Make the two 'rules'.. First one, simple 1/0 flag.
					// - Get the true/false first.
					bool isSpam = false;
					if (bool.TryParse(matches[0].Groups["is_spam"].Value.ToLowerInvariant(), out isSpam))
					{
						//Get the total reported score.
						double tryScore = 0.0;
						if (double.TryParse(matches[0].Groups["spam_level"].Value, out tryScore))
						{
							//Add the rule!
							results.Add(new RuleResult(tryScore, "SPAM_LEVEL", line));
						}

						//Now add the flag entry.
						results.Add(new RuleResult(((isSpam) ? 1D : 0D), "IS_SPAM", line));
					}
				}

				//Now see if the line matches a rules entry..
				if (REGEX_RULELINE.IsMatch(line))
				{
					//Parse line.
					MatchCollection matches = REGEX_RULELINE.Matches(line);

					//Get the values..
					double ruleScore = 0.0;
					if (double.TryParse(matches[0].Groups["score"].Value, out ruleScore))
					{
						//The name..
						string ruleName = matches[0].Groups["rulename"].Value.Trim();

						//The description. Will have to add following lines if necessary.
						string ruleDesc = matches[0].Groups["rule_desc"].Value.Trim();
						for (int j = 1; j <= 5; j++) //Check the next 5 lines.
						{
							if ((i + j) < lines.Length) //Don't go past end of array.
							{
								string nextLine = lines[i + j].Trim();
								if (REGEX_RULELINE.IsMatch(lines[i + j].Trim())) //It matches, so abort.
								{
									i += (j - 1); //Fast-gorward main loop.
									j = 6; //Abort this loop.
								}
								else //It doesn't match..
								{
									if (!string.IsNullOrWhiteSpace(nextLine)) //And the line's not empty.
									{
										//Add it to the description.
										ruleDesc += " " + nextLine;
									}
								}
							}
							else
							{
								j = 6;
							}
						}

						//Save it to the list.
						results.Add(new RuleResult(ruleScore, ruleName, ruleDesc));
					}
				}
			}
		}

		return results;
	}
}