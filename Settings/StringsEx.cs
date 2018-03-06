using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using Inflectra.KronoDesk.Service.Email.Settings.Sgml;
using System.Text.RegularExpressions;
using System.Web;

namespace Inflectra.KronoDesk.Service.Email.Settings
{
	public static class StringsEx
	{
		private static string EMAIL_LINEBREAK = "\r\n";

		public static string ToSafeString(this object str)
		{
			if (str == null)
				return "";
			else
				return str.ToString();
		}

		public static string ToHTML(this string str)
		{
			//Simply replace newlines with <br />'s..
			return str.Replace(Environment.NewLine, "<br />")
				.Replace("\r\n", "<br />")
				.Replace("\n\r", "<br />")
				.Replace("\n", "<br />")
				.Replace("\r", "<br />");
		}

		/// <summary>Fixes HTML text to make sure all the tags have closing elements, etc.</summary>
		/// <param name="input">the input text</param>
		/// <returns>The 'fixed html'</returns>
		public static string ToFixedHtml(this string input)
		{
			//The return string..
			string retString = "";

			//Try loading as SGML
			//string htmlDoc = input.Trim();
			//if (!htmlDoc.Contains("<html"))
			//    htmlDoc = "<html>" + input + "</html>";

			//Strip any DOCTYPE..
			//string htmlDoc = Regex.Replace(input.Trim(), @"\<!DOCTYPE .*?\>", "");
			string htmlDoc = input.Trim();

			//Get the URI for the DTD file..
			Uri dtdLocation = new Uri(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "dtd\\xhtml1-transitional.dtd"));

			//Logger.LogTraceEvent(CLASS_NAME + METHOD_NAME, htmlDoc);
			StringReader stringReader = new StringReader(htmlDoc.Trim());
			SgmlReader sgmlReader = new SgmlReader();
			XmlDocument xhtmlDocument = new XmlDocument();

			try
			{
				sgmlReader.DocType = "HTML";
				sgmlReader.SystemLiteral = dtdLocation.ToString();
				sgmlReader.WhitespaceHandling = WhitespaceHandling.All;
				sgmlReader.CaseFolding = CaseFolding.ToLower;
				sgmlReader.InputStream = stringReader;

				//Now create the new XML document from this (i.e. XHTML 1.0)
				xhtmlDocument.PreserveWhitespace = true;
				xhtmlDocument.XmlResolver = null;
				xhtmlDocument.Load(sgmlReader);
			}
			catch (Exception)
			{
				//Try adding a div around things..
				htmlDoc = "<div>" + htmlDoc + "</div>";
				sgmlReader.DocType = "HTML";
				sgmlReader.SystemLiteral = dtdLocation.ToString();
				sgmlReader.WhitespaceHandling = WhitespaceHandling.All;
				sgmlReader.CaseFolding = CaseFolding.ToLower;
				sgmlReader.InputStream = stringReader;

				//Now create the new XML document from this (i.e. XHTML 1.0)
				xhtmlDocument.PreserveWhitespace = true;
				xhtmlDocument.XmlResolver = null;
				xhtmlDocument.Load(sgmlReader);
			}

			if (xhtmlDocument.GetElementsByTagName("body").Count >= 1) //First try to get the <body> tag.
				retString = xhtmlDocument.GetElementsByTagName("body")[0].InnerXml;
			else if (xhtmlDocument.GetElementsByTagName("html").Count >= 1) //No <body>, try for <html> only?
				retString = xhtmlDocument.GetElementsByTagName("html")[0].InnerXml;
			else //No <html> wither. return the whole thing.
				retString = xhtmlDocument.InnerXml;

			return retString;
		}

		/// <summary>Strips MSWord tags form an input string.</summary>
		/// <param name="input">The string to strip HTML tags from.</param>
		/// <returns>A clean string.</returns>
		public static string StripWORD(this string input)
		{
			if (input != null)
			{
				input = Regex.Replace(input, "(?is)<!--\\[if gte mso \\d{0,3}\\]>.*?(<!\\[endif\\]-->)", ""); //Removes MSW IF/ENDIF format.
				input = Regex.Replace(input, "(?is)<!--\\[if gte vml \\d{0,3}\\]>.*?(<!\\[endif\\]-->)", ""); //Removes MSW VML objects.
				input = Regex.Replace(input, "(?is)<!--\\[if \\!vml\\]>.*?(<!\\[endif\\]-->)", ""); //Removes MSW non-VML images.
				input = Regex.Replace(input, "(?is)<meta[^>]*?>", ""); // Removes meta tags.
				input = Regex.Replace(input, "(?is)<link[^>]*?>", ""); // Removes link tags.
				input = Regex.Replace(input, "(?is)<o:([^>]*)>.*?</o:\\1>", ""); // Removes Office-Specific tags.
				input = Regex.Replace(input, "(?is)<!--.*?-->", ""); //Remove any last HMTL comment tags.
			}

			return input;
		}

		public static string StripHTML(this string inputString, bool addHrefs = true, bool filterWord = true)
		{
			if (inputString == null)
			{
				return "";
			}

			string retString = inputString;
			//First see if they want to add Hrefs, and get the links from <a> tags..
			if (addHrefs)
			{
				//Get a list of all <a></a> tags..
				Regex ahrefEdit = new Regex("<[ ]?a.*? href=(\"?)(?<url>(?:ftp|http|https):\\/\\/(?:\\w+:{0,1}\\w*@)?(?:\\S+)(?:\\:[0-9]+)?(?:\\/|\\/(?:[\\w#!:.?+=&%@!\\-\\/]))?)\\1.*?(?:.|\\n)*?</a>");
				MatchCollection ahrefs = ahrefEdit.Matches(inputString);
				//Loop through and replace it..
				foreach (Match aHref in ahrefs)
					retString = retString.Replace(aHref.Groups[0].Value, aHref.Groups[0].Value + " [" + aHref.Groups[2].Value + "]");
			}

			if (filterWord)
				retString = retString.StripWORD();

			retString = retString.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");
			retString = retString.Replace("\t", "");
			retString = Regex.Replace(retString, @"( )+", " ");

			// Remove the header (prepare first by clearing attributes)
			retString = Regex.Replace(retString, "(<( )*head([^>])*>).*(<( )*head([^>])*>)", "", RegexOptions.IgnoreCase);

			// remove all scripts (prepare first by clearing attributes)
			retString = Regex.Replace(retString, @"(<( )*script([^>])*>).*(<( )*(/)( )*script( )*>)", "", RegexOptions.IgnoreCase);

			// remove all styles (prepare first by clearing attributes)
			retString = Regex.Replace(retString, "(<( )*style([^>])*>).*(<( )*(/)( )*style( )*>)", "", RegexOptions.IgnoreCase);

			// insert tabs in spaces of <td> tags
			retString = Regex.Replace(retString, @"<( )*td([^>])*>", "\t", RegexOptions.IgnoreCase);

			// insert line breaks in places of <BR> and <LI> tags
			retString = Regex.Replace(retString, @"<( )*br( )*(/)?>", EMAIL_LINEBREAK, RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, @"<( )*li( )*>", EMAIL_LINEBREAK + "* ", RegexOptions.IgnoreCase);

			// insert line paragraphs (double line breaks) in place
			// if <P>, <DIV> and <TR> tags
			retString = Regex.Replace(retString, @"<( )*div([^>])*>", EMAIL_LINEBREAK + EMAIL_LINEBREAK, RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, @"<( )*tr([^>])*>", EMAIL_LINEBREAK + EMAIL_LINEBREAK, RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, @"<( )*p([^>])*>", EMAIL_LINEBREAK + EMAIL_LINEBREAK, RegexOptions.IgnoreCase);

			// Remove remaining tags like <a>.
			retString = Regex.Replace(retString, @"<[^>]*>", "", RegexOptions.IgnoreCase);

			// replace special characters:
			retString = Regex.Replace(retString, "&nbsp;", " ", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "&bull;", " * ", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "&lsaquo;", "<", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "&rsaquo;", ">", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "&laquo;", "«", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "&raquo;", "»", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "&trade;", "(tm)", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "&copy;", "(C)", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "&frasl;", "/", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "&lt;", "<", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "&gt;", ">", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "&reg;", "(R)", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "&deg;", "°", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "&cent;", "¢", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "&pound;", "£", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "&curren;", "¤", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "&yen;", "¥", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "&brvbar;", "|", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "&(n|m)dash;", "-", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "&euro;", "€", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "&empty;", "∅", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "&sim;", "~", RegexOptions.IgnoreCase);
			//Very last, amps..
			retString = Regex.Replace(retString, "&amp;", "&", RegexOptions.IgnoreCase);
			//Clear out what's left.
			retString = Regex.Replace(retString, @"&(.{2,6});", "", RegexOptions.IgnoreCase);

			//Trim Linebreaks & Spaces
			retString = Regex.Replace(retString, "(\r\n)( )+(\r\n)", EMAIL_LINEBREAK + EMAIL_LINEBREAK, RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "(\t)( )+(\t)", "\t\t", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "(\t)( )+(\r\n)", "\t" + EMAIL_LINEBREAK, RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "(\r\n)( )+(\t)", EMAIL_LINEBREAK + "\t", RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "(\r\n)(\t)+(\r\n)", EMAIL_LINEBREAK, RegexOptions.IgnoreCase);
			retString = Regex.Replace(retString, "(\r\n)(\t)+", EMAIL_LINEBREAK + "\t", RegexOptions.IgnoreCase);

			//Reduce anything more than four linebreaks to three.
			string limitRet = EMAIL_LINEBREAK + EMAIL_LINEBREAK + EMAIL_LINEBREAK + EMAIL_LINEBREAK;
			string maxRet = EMAIL_LINEBREAK + EMAIL_LINEBREAK + EMAIL_LINEBREAK;
			while (retString.Contains(limitRet))
				retString = retString.Replace(limitRet, maxRet);
			//Reduce any more than 5 tabs to 5 tabs.
			string limitTab = "\t\t\t\t\t\t";
			string maxTab = "\t\t\t\t\t";
			while (retString.Contains(limitTab))
				retString = retString.Replace(limitTab, maxTab);

			return retString;
		}

	}
}
