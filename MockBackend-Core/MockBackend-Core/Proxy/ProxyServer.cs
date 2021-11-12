using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Text;

namespace MockBackend_Core.Proxy
{
    public class ProxyServer
    {
        const string LINE_SEPARATOR = "\r\n";
        static ManualResetEvent clientConnected = new ManualResetEvent(false);
        readonly TcpListener server;
        private Thread? listenerThread;
        public ProxyServer(string listenOnHost, int listenOnPort)
        {
            server = new TcpListener(IPAddress.Parse(listenOnHost), listenOnPort);
        }

        public bool Start(out string errorMsg)
        {
            errorMsg = "";

            try
            {
                server.Start();
                listenerThread = new Thread(() =>
                {
                    Console.WriteLine($"Proxy listener started on {server.LocalEndpoint}");
                    while (true)
                    {

                        clientConnected.Reset();
                        server.BeginAcceptSocket(new AsyncCallback(HandleIncomingRequest), server);
                        clientConnected.WaitOne();
                        /*
                                            Console.WriteLine("Connection accepted.");

                                            Console.WriteLine("Reading data...");

                                            byte[] data = new byte[100];
                                            int size = client.Receive(data);
                                            Console.WriteLine("Recieved data: ");
                                            for (int i = 0; i < size; i++)
                                                Console.Write(Convert.ToChar(data[i]));

                                            Console.WriteLine();

                                            client.Close();
                        */
                    }
                });
                listenerThread.Start();
                return true;
            }
            catch (Exception e)
            {
                errorMsg = e.Message;
                return false;
            }
        }

        public bool Stop(out string errorMsg)
        {
            errorMsg = "";

            try
            {
                listenerThread?.Join(500);
                server.Stop();
                return true;
            }
            catch (Exception e)
            {
                errorMsg = e.Message;
                return false;
            }
        }

        static void HandleIncomingRequest(IAsyncResult ar)
        {
            TcpListener listener = (TcpListener?)ar.AsyncState ?? throw new Exception("Async result was null");
            Socket clientSocket = listener.EndAcceptSocket(ar);
            Console.WriteLine($"Incoming client {clientSocket.RemoteEndPoint}");
            clientConnected.Set();

            // Process the connection here. (Add the client to a
            // server table, read data, etc.)
            byte[] buffer = new byte[32];
            string tmp = "";

            while (clientSocket.Available > 0)
            {
                int gotBytes = clientSocket.Receive(buffer);
                tmp += Encoding.UTF8.GetString(buffer, 0, gotBytes);                
            }

            List<string> requestHeaders = new();

            // Dirty split, RFC 5322 section 2.2 states more rules for identifying a header, TODO
            string[] rawContent = tmp.Split(LINE_SEPARATOR);
            bool receivingBody = false;
            string body = "";
            for (int i = 0; i < rawContent.Length; i++)
            {
                string line = rawContent[i];

                if (!receivingBody && line == "")
                {
                    receivingBody = true;
                    continue;
                }

                if (receivingBody)
                {
                    body += line + LINE_SEPARATOR;
                }
                else
                {
                    requestHeaders.Add(line);
                }
            }

            if (body.EndsWith(LINE_SEPARATOR))
                body = body.Remove(body.Length - LINE_SEPARATOR.Length);

            if (requestHeaders.Count > 0)
            {
                // RFC 7230 section 3.0 states the start line is always at the beginning
                // RFC 7230 section 3.1.1 specifies the request line format as [method SP request-target SP HTTP-version CRLF]
                string[] requestStart = requestHeaders[0].Split(' ');
                requestHeaders.RemoveAt(0);
                string method = requestStart[0];
                string host = requestStart[1];
                string httpVersion = requestStart[2].ToUpper();

                // TODO look for pattern match and intercept request or forward to the intended endpoint and return the response to the client
                HttpWebRequest forwardedRequest = WebRequest.CreateHttp(host);
                forwardedRequest.Method = method;
                forwardedRequest.ProtocolVersion = Version.Parse(httpVersion[5..]);
                forwardedRequest.Timeout = 5000;
                if (body != "")
                {
                    using StreamWriter writer = new(forwardedRequest.GetRequestStream());
                    writer.Write(body);
                }
                requestHeaders.ForEach(header => forwardedRequest.Headers.Add(header));

                int statusCode = -1;
                string reason = "";
                HttpWebResponse? forwardedResponse;
                try {
                    forwardedResponse = (HttpWebResponse)forwardedRequest.GetResponse();
                    statusCode = (int)forwardedResponse.StatusCode;
                    reason = forwardedResponse.StatusDescription;
                }
                catch (WebException exception)
                {
                    forwardedResponse = (HttpWebResponse?)exception.Response;
                    statusCode = (int?)forwardedResponse?.StatusCode ?? -1;
                    reason = forwardedResponse?.StatusDescription ?? "Timeout";
                }


                string responseLine = $"{httpVersion} {statusCode} {reason}{LINE_SEPARATOR}";
                if (forwardedResponse != null)
                {
                    foreach (string header in forwardedResponse.Headers)
                    {
                        // Header specification in RFC 7230 section 3.2
                        responseLine += $"{header}: {forwardedResponse.Headers[header]}{LINE_SEPARATOR}";
                    }

                    responseLine += LINE_SEPARATOR;
                    clientSocket.Send(Encoding.UTF8.GetBytes(responseLine));

                    /*using BinaryReader responseStream = new(forwardedResponse.GetResponseStream());

                    if (responseBody != "")
                    {
                        responseLine += responseBody + LINE_SEPARATOR;
                    }*/
                    using BinaryReader responseStream = new(forwardedResponse.GetResponseStream());
                    while (responseStream.PeekChar() > -1)
                    {
                        clientSocket.Send(responseStream.ReadBytes(32));
                    }
                    clientSocket.Send(Encoding.UTF8.GetBytes(LINE_SEPARATOR));
                }

              /*  if (forwardedResponse != null)
                {
                    using BinaryReader responseStream = new(forwardedResponse.GetResponseStream());
                    while (responseStream.PeekChar() > -1)
                    {
                        clientSocket.Send(responseStream.ReadBytes(32));
                    }
                }*/
                //clientSocket.Send(Encoding.UTF8.GetBytes("\r\n"));
            }
            
            clientSocket.Close();
        }
    }
}
