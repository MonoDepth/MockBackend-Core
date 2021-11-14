using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Net.Security;
using MockBackend_Core.Extensions;

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

            // Process the connection here. (Add the client to a server table, read data, etc.)
            List<string> requestHeaders = new();

            string rawClientRequestData = GetClientResponse(clientSocket);

            // Dirty split, RFC 5322 section 2.2 states more rules for identifying a header, TODO
            string[] rawContent = rawClientRequestData.Split(LINE_SEPARATOR);
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
                // TODO HTTPS is broken, only HTTP works right now
                bool isConnectRequest = method == "CONNECT";
                if (host.Contains("://"))
                {
                    host = host.Remove(0, host.IndexOf("://") + 3);
                }
                string[] splitHost = host.Split(':');
                int port = splitHost.Length > 1 ? int.Parse(splitHost[1]) : 80;
                TcpClient forwardedRequest = new();
                forwardedRequest.Connect(splitHost[0].TrimEnd('/'), port);
                Stream forwardedRequestStream = /*isConnectRequest ? new SslStream(forwardedRequest.GetStream()) : */ forwardedRequest.GetStream();

                // Could be used to decrypt the traffic before sending it to the server
                // But in the forward case we don't care about the content and only acting as a passthrough proxy
                // Decrypting would require a locally trusted homemade certificate

                /*if (forwardedRequestStream is SslStream sslStream)
                {
                    sslStream.AuthenticateAsClient(splitHost[0].TrimEnd('/'));
                }
                */

                // If we're getting a CONNECT request we're going to open a tunnel to the server and signal the client to start sending the encrypted data
                if (isConnectRequest)
                {
                    //See RFC 7230 section 3.1.2 for the status-line response format
                    //HTTP-version SP status-code SP reason-phrase CRLF
                    string responseLine = $"{httpVersion} 200 OK{LINE_SEPARATOR}{LINE_SEPARATOR}";
                    clientSocket.Send(responseLine.AsBytes());
                    TunnelRequest(clientSocket, forwardedRequestStream);
                }
                else
                {
                    forwardedRequestStream.Write(rawClientRequestData.AsBytes());
                    Stopwatch timeoutStopWatch = new();
                    timeoutStopWatch.Start();
                    while (forwardedRequestStream.CanSeek && forwardedRequestStream.Length == 0 && timeoutStopWatch.ElapsedMilliseconds < 60000)
                    {
                        Thread.Sleep(100);
                    }
                    timeoutStopWatch.Stop();
                
                    if (forwardedRequestStream.CanRead)
                    {
                        ForwardRequestToClient(clientSocket, forwardedRequestStream);
                    }
                    else
                    {
                        string responseLine = $"{httpVersion} 504 Gateway Timeout{LINE_SEPARATOR}{LINE_SEPARATOR}";
                        clientSocket.Send(responseLine.AsBytes());
                    }
                }
                forwardedRequestStream.Close();
            }

            clientSocket.Close();            
        }

        private static string GetClientResponse(Socket client)
        {
            byte[] buffer = new byte[32];
            string rawClientRequestData = "";
            while (client.Available > 0)
            {
                int gotBytes = client.Receive(buffer);
                rawClientRequestData += buffer.AsString(gotBytes);
            }
            return rawClientRequestData;
        }

        private static void TunnelRequest(Socket client, Stream server)
        {
            while (client.Available > 0)
            {
                string clientResponse = GetClientResponse(client);
                server.Write(clientResponse.AsBytes());
                server.Flush();
                ForwardRequestToClient(client, server);
            }
        }

        private static void ForwardRequestToClient(Socket client, Stream server)
        {
            byte[] buffer;
            int bytesRead;

            if (!server.CanRead)
            {
                return;
            }
           
            do
            {
                buffer = new byte[256];
                bytesRead = server.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    string tmp = buffer.AsString(bytesRead);
                    client.Send(buffer, bytesRead, SocketFlags.None);
                }
            } while (bytesRead > 0);
        }
    }
}
