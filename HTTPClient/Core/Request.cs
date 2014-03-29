using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace HttpClient.Core
{
	public class Request
	{
        public Uri Uri { get; set; }
        public string Method { get; set; }
        public string Version { get; set; }
		private Dictionary<string, string> getParameters = new Dictionary<string, string>();
		private Dictionary<string, string> postParameters = new Dictionary<string, string>();
		private Dictionary<string, string> headers = new Dictionary<string, string>();
		 
		public Request (string url)
		{
			this.Uri = new Uri (url);
			this.Method = HTTPMethod.GET;
			this.Version = HTTPVersion.HTTP11;
			this.AddHeader ("Host", this.Uri.Host);
		}

		public Request (string url, string method)
		{
			this.Uri = new Uri (url);
			this.Method = method;
			this.Version = HTTPVersion.HTTP11;
			this.AddHeader ("Host", this.Uri.Host);
		}

		public Request (string url, string method, string version)
		{
			this.Uri = new Uri (url);
			this.Method = method;
			this.Version = version;
			this.AddHeader ("Host", this.Uri.Host);
		}

		public void AddHeader(string header, string value)
		{
			this.headers.Add (header, value);
		}

		public void ClearHeaders()
		{
			this.headers.Clear ();
		}

		public void AddGetParameter(string parameter, string value)
		{
			this.getParameters.Add (parameter, value);
		}

		public void ClearGetParameters()
		{
			this.getParameters.Clear ();
		}

		public void AddPostParameter(string parameter, string value)
		{
			this.postParameters.Add (parameter, value);
		}

		public void ClearPostParameters()
		{
			this.postParameters.Clear ();
		}
	
		public override string ToString()
		{
			StringBuilder request = new StringBuilder ();

			request.Append (CreateRequestLine ());
			request.Append (CreateHeaders ());
			request.Append (CreatePostParameters ());

			return request.ToString ();
		}

		private string CreateRequestLine()
		{
			return this.Method + ' ' + this.Uri.AbsolutePath + this.CreateGetParameters() + ' ' + this.Version + "\r\n";
		}

		private string CreateHeaders()
		{
			StringBuilder headers = new StringBuilder();
			foreach (KeyValuePair<string, string> header in this.headers)
				headers.Append (header.Key + ": " + header.Value + "\r\n");
			headers.Append ("\r\n");
			return headers.ToString();
		}

		private string CreateGetParameters()
		{
			StringBuilder getParameters = new StringBuilder ();
			int count = 0;

			foreach (KeyValuePair<string, string> getParameter in this.getParameters)
			{
				if (count != this.postParameters.Count - 1)
					getParameters.Append (getParameter.Key + '=' + getParameter.Value + '&');
				else
					getParameters.Append (getParameter.Key + '=' + getParameter.Value);
				count++;
			}

			return getParameters.ToString ();
		}

		private string CreatePostParameters()
		{
			StringBuilder postParameters = new StringBuilder ();
			int count = 0;

			foreach (KeyValuePair<string, string> postParameter in this.postParameters)
			{
				if (count != this.postParameters.Count - 1)
					postParameters.Append (postParameter.Key + '=' + postParameter.Value + '&');
				else
					postParameters.Append (postParameter.Key + '=' + postParameter.Value);
				count++;
			}

			return postParameters.ToString ();
		}
	}
}

