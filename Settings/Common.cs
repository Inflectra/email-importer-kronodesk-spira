using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Inflectra.KronoDesk.Service.Email.Settings
{
	public static class Common
	{
		//App Name.
		public const string APP_NAME = "InflectraEmailPoll";

		public const string APP_DIRECTORY = "EmailIntegration";
		public const string APP_MANUF = "Inflectra";

		//Login sufficies.
		public const string LOGINSUF_SPIRA = "/Login.aspx";
		public const string LOGINSUF_KRONO = "/Account/Login.aspx";

		//Settings file.
		public const string SETTINGS_FILE = "configuration.conf";

		//String used in forms..
		public const string LABEL_DEFAULT_PROJECT = "Default Project:";
		public const string LABEL_DEFAULT_PRODUCT = "Default Product:";

		//Header fields..
		public const string MESSAGEHEADER_SPIRA_ARTIFACT = "X-Inflectra-Spira-Art";
		public const string MESSAGEHEADER_SPIRA_DATE = "X-Inflectra-Spira-Dte";
		public const string MESSAGEHEADER_SPIRA_USERTO = "X-Inflectra-Spira-UsrTo";
		public const string MESSAGEHEADER_SPIRA_PROJECT = "X-Inflectra-Spira-Prj";
		public const string MESSAGEHEADER_KRONO_TICKET = "X-Inflectra-Krono-Tkt";
		public const string MESSAGEHEADER_KRONO_DATE = "X-Inflectra-Krono-Dte";
		public const string MESSAGEHEADER_KRONO_USERTO = "X-Inflectra-Krono-UsrTo";
		public const string MESSAGEHEADER_KRONO_PRODUCT = "X-Inflectra-Krono-Prd";

		//Defaults for settings file.
		public const bool SETTINGS_DEFAULT_TRACE = false; //Tracelogging off.
		public const int SETTINGS_DEFAULT_POLLINTERVAL = 10; //10 minutes.
	}
}
