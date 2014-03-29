using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace HttpClient.Core
{
	public class HttpClient
	{
		private Request request = null;
		private Response response = null;
		 
		public HttpClient(Request request)
		{
            this.request = request;
		}

		public Response Request()
		{
            
			TcpClient client = null;
			NetworkStream stream = null;
			StreamWriter writer = null;
			StreamReader reader = null;

            client = new TcpClient(this.request.Uri.Host, this.request.Uri.Port);
            
            if (client == null)
                return null;

			stream = client.GetStream();
            if (stream == null)
                return null;

            writer = new StreamWriter(stream);
			writer.Write(this.request.ToString());
            writer.Flush();

            reader = new StreamReader(stream);
            this.response = new Response(reader.ReadToEnd());

			return this.response;
		}
	}
}

