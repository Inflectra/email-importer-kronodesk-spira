using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using Inflectra.KronoDesk.Service.Email.Settings;
using Inflectra.KronoDesk.Service.Email.Settings.KronoClient;

namespace Inflectra.KronoDesk.Service.Email.UI
{
	internal class Thread_Krono_GetProducts
	{
		#region Public Properties and Events
		/// <summary>Fired off once the import is finished (or cancelled.)</summary>
		public event EventHandler<KronoFinishArgs> ProgressFinished;
		#endregion

		private const string CLASS_NAME = "Thread_Krono_GetProducts.";

		private string _serverURL;
		private string _userName;
		private string _userPass;

		/// <summary>Creates a new instance of the thread to get projects rom Spirateam.</summary>
		/// <param name="ServerURL">The URO of the Spirateam API.</param>
		/// <param name="UserName"></param>
		/// <param name="UserPassword"></param>
		public Thread_Krono_GetProducts(string ServerURL, string UserName, string UserPassword)
		{
			this._serverURL = ServerURL;
			this._userName = UserName;
			this._userPass = UserPassword;
		}

		/// <summary>Starts the thread.</summary>
		public void StartProcess()
		{
			try
			{
				//Connect to the server, get 
				Settings.KronoClient.SoapServiceClient client = Settings.ClientFactory.CreateClient_Krono(new Uri(this._serverURL + "/" + ClientFactory.KRONO_API));

				//Log in..
				if (client.Connection_Authenticate(this._userName, this._userPass, Common.APP_NAME,true))
				{
					//Okay, try to get the list of project. 
					List<RemoteProduct> prods = client.Product_Retrieve(false);

					//Got projects? Return 'em!
					if (this.ProgressFinished != null)
					{
						this.ProgressFinished(this, new KronoFinishArgs(prods));
					}
				}
				else
				{
					if (this.ProgressFinished != null)
					{
						this.ProgressFinished(this, new KronoFinishArgs(new Exception("Could not log in with username and password! Check your application server definition and try again.")));
					}
				}
			}
			catch (Exception ex)
			{
				if (this.ProgressFinished != null)
				{
					this.ProgressFinished(this, new KronoFinishArgs(new Exception("Could not connect to the server. Check your application server definition and try again.", ex)));
				}
			}
		}
	}

	/// <summary>Class for holding the projects or error.</summary>
	public class KronoFinishArgs : EventArgs
	{
		public KronoFinishArgs(Exception ex)
		{
			this.Error = ex;
		}

		public KronoFinishArgs(List<RemoteProduct> products)
		{
			this.Products = products;
		}

		public Exception Error
		{ get; set; }

		public List<RemoteProduct> Products
		{ get; set; }
	}
}
