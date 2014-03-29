using System;
using System.Collections.Generic;
using System.Text;

namespace HttpClient.Core
{
	public class Response
	{
		public string Version { get; set; }
		public int Code { get; set; }
		public string Message { get; set; }
		public string Body { get; set; }
		private IDictionary<string, string> headers = new Dictionary<string, string> ();

		public Response (string response)
		{
			this.StartResponseLine (response);
			this.StartHeaders (response);
			this.StartBody (response);
		}

        public Response(string version, int code, string message)
        {
            this.Version = version;
            this.Code = code;
            this.Message = message;
        }

		public void AddHeader(string header, string value)
		{
			this.headers.Add (header, value);
		}

		public void ClearHeaders()
		{
			this.headers.Clear ();
		}

		public override string ToString ()
		{
			StringBuilder response = new StringBuilder ();
			response.Append (CreateResponseLine ());
			response.Append (CreateHeaders ());
			response.Append (this.Body);
			return response.ToString ();
		}

		private string[] DivideResponse(string response)
		{
			return response.Split (new string[] { "\r\n\r\n" }, StringSplitOptions.None); 
		}

		private void StartResponseLine(string response)
		{
			string line = DivideResponse(response)[0].Split (new string[] { "\r\n" }, StringSplitOptions.None)[0];
			string[] words = line.Split (new string[] { " " }, 3, StringSplitOptions.None);
			this.Version = words [0];
			this.Code = int.Parse(words [1]);
			this.Message = words [2];
		}

		private void StartHeaders(string response)
		{
			string[] headers = DivideResponse (response)[0].Split(new string[] { "\r\n" }, StringSplitOptions.None);
			for (int i = 1; i < headers.Length; i++)
			{
				string[] header = headers[i].Split (new string[] { ": " }, StringSplitOptions.None);
				this.AddHeader (header [0], header [1]);
			}
		}

		private void StartBody(string response)
		{
			this.Body = DivideResponse (response) [1];
		}

		private string CreateResponseLine()
		{
			StringBuilder responseLine = new StringBuilder ();
			return responseLine.Append (this.Version)
							   .Append (' ')
							   .Append (this.Code)
							   .Append (' ')
							   .Append (this.Message)
							   .Append ("\r\n")
							   .ToString();
		}

		private string CreateHeaders()
		{
			StringBuilder responseHeaders = new StringBuilder ();

			foreach (KeyValuePair<string, string> header in this.headers)
				responseHeaders.Append (header.Key)
							   .Append (": ")
							   .Append (header.Value)
							   .Append ("\r\n");
			responseHeaders.Append ("\r\n");

			return responseHeaders.ToString ();
		}
	}
}

