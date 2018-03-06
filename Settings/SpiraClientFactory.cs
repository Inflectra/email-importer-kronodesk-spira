using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace Inflectra.KronoDesk.Service.Email.Settings
{
	/// <summary>
	/// Factory for creating and configuring new Spira API endpoints
	/// </summary>
	public static class ClientFactory
	{
		public static string SPIRA_API = "/Services/v4_0/ImportExport.svc";
		public static string KRONO_API = "/Services/v1_0/ImportExport.svc";

		/// <summary>Creates the WCF endpoints</summary>
		/// <param name="fullUri">The URI</param>
		/// <returns>The client class</returns>
		/// <remarks>We need to do this in code because the app.config file is not available in VSTO</remarks>
		public static SpiraClient.ImportExportClient CreateClient_Spira(Uri fullUri)
		{
			BasicHttpBinding httpBinding = ClientFactory.createBinding(fullUri.Scheme);

			//Create the new client with endpoint and HTTP Binding
			EndpointAddress endpointAddress = new EndpointAddress(fullUri.AbsoluteUri);
			SpiraClient.ImportExportClient spiraImportExport = new SpiraClient.ImportExportClient(httpBinding, endpointAddress);

			//Modify the operation behaviors to allow unlimited objects in the graph
			foreach (var operation in spiraImportExport.Endpoint.Contract.Operations)
			{
				var behavior = operation.Behaviors.Find<DataContractSerializerOperationBehavior>() as DataContractSerializerOperationBehavior;
				if (behavior != null)
				{
					behavior.MaxItemsInObjectGraph = 2147483647;
				}
			}
			return spiraImportExport;
		}

		/// <summary>Creates the WCF endpoints</summary>
		/// <param name="fullUri">The URI</param>
		/// <returns>The client class</returns>
		/// <remarks>We need to do this in code because the app.config file is not available in VSTO</remarks>
		public static KronoClient.ImportExportClient CreateClient_Krono(Uri fullUri)
		{
			BasicHttpBinding httpBinding = ClientFactory.createBinding(fullUri.Scheme);

			//Create the new client with endpoint and HTTP Binding
			EndpointAddress endpointAddress = new EndpointAddress(fullUri.AbsoluteUri);
			KronoClient.ImportExportClient kronoImportExport = new KronoClient.ImportExportClient(httpBinding, endpointAddress);

			//Modify the operation behaviors to allow unlimited objects in the graph
			foreach (var operation in kronoImportExport.Endpoint.Contract.Operations)
			{
				var behavior = operation.Behaviors.Find<DataContractSerializerOperationBehavior>() as DataContractSerializerOperationBehavior;
				if (behavior != null)
				{
					behavior.MaxItemsInObjectGraph = 2147483647;
				}
			}
			return kronoImportExport;
		}

		/// <summary>Creates the Basic Binding to be used in the client.</summary>
		/// <returns></returns>
		private static BasicHttpBinding createBinding(string scheme)
		{
			BasicHttpBinding retBinding = new BasicHttpBinding();

			//Allow cookies and large messages
			retBinding.AllowCookies = true;
			retBinding.MaxBufferSize = Int32.MaxValue;
			retBinding.MaxBufferPoolSize = Int32.MaxValue;
			retBinding.MaxReceivedMessageSize = Int32.MaxValue;
			retBinding.ReaderQuotas.MaxStringContentLength = Int32.MaxValue;
			retBinding.ReaderQuotas.MaxDepth = Int32.MaxValue;
			retBinding.ReaderQuotas.MaxBytesPerRead = Int32.MaxValue;
			retBinding.ReaderQuotas.MaxNameTableCharCount = Int32.MaxValue;
			retBinding.ReaderQuotas.MaxArrayLength = Int32.MaxValue;
			retBinding.CloseTimeout = new TimeSpan(0, 2, 0);
			retBinding.OpenTimeout = new TimeSpan(0, 2, 0);
			retBinding.ReceiveTimeout = new TimeSpan(0, 2, 0);
			retBinding.SendTimeout = new TimeSpan(0, 2, 0);
			retBinding.BypassProxyOnLocal = false;
			retBinding.HostNameComparisonMode = HostNameComparisonMode.StrongWildcard;
			retBinding.MessageEncoding = WSMessageEncoding.Text;
			retBinding.TextEncoding = System.Text.Encoding.UTF8;
			retBinding.TransferMode = TransferMode.Buffered;
			retBinding.UseDefaultWebProxy = true;
			retBinding.Security.Mode = BasicHttpSecurityMode.None;
			retBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.None;
			retBinding.Security.Transport.ProxyCredentialType = HttpProxyCredentialType.None;
			retBinding.Security.Message.ClientCredentialType = BasicHttpMessageCredentialType.UserName;
			retBinding.Security.Message.AlgorithmSuite = System.ServiceModel.Security.SecurityAlgorithmSuite.Default;

			//Handle SSL if necessary
			if (scheme == "https")
			{
				retBinding.Security.Mode = BasicHttpSecurityMode.Transport;
				retBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.None;

				//Allow self-signed certificates
				PermissiveCertificatePolicy.Enact("");
			}
			else
			{
				retBinding.Security.Mode = BasicHttpSecurityMode.None;
			}

			return retBinding;
		}
	}

	/// <summary>Allows the use of Self-Signed SSL certificates with the data-sync</summary>
	public class PermissiveCertificatePolicy
	{
		string subjectName = "";
		static PermissiveCertificatePolicy currentPolicy;

		PermissiveCertificatePolicy(string subjectName)
		{
			this.subjectName = subjectName;
			ServicePointManager.ServerCertificateValidationCallback += new System.Net.Security.RemoteCertificateValidationCallback(RemoteCertValidate);
		}

		public static void Enact(string subjectName)
		{
			currentPolicy = new PermissiveCertificatePolicy(subjectName);
		}


		bool RemoteCertValidate(object sender, X509Certificate cert, X509Chain chain, System.Net.Security.SslPolicyErrors error)
		{
			if (cert.Subject == subjectName || subjectName == "")
				return true;
			else
				return false;
		}
	}
}
