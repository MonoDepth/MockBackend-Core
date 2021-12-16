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
        
        public int MockBackendHttpPort { get; set; }
        public int MockBackendHttpsPort { get; set; } 
        public List<string> DomainsToRelay { get; set; }  = new List<string>();

        public ProxyServer(string listenOnHost, int listenOnPort)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
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

        void HandleIncomingRequest(IAsyncResult ar)
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
                //Try parsing the host from the host header, otherwise fallback to the required start line
                string host = rawContent.FirstOrDefault(header => header.ToLower().StartsWith("host:"))?.Remove(0, 5).Trim() ?? SanitizeEndpointFromStartLine(requestStart[1]);
                string httpVersion = requestStart[2].ToUpper();

                // TODO look for pattern match and intercept request or forward to the intended endpoint and return the response to the client
                // TODO HTTPS is broken, only HTTP works right now
                bool isConnectRequest = method == "CONNECT";
                string[] splitHost = host.Split(':');
                string domain = splitHost[0];
                int port = splitHost.Length > 1 ? int.Parse(splitHost[1]) : 80;

                if (DomainsToRelay.Any(x => x.Equals(domain, StringComparison.OrdinalIgnoreCase)))
                {
                    domain = "127.0.0.1";
                    port = isConnectRequest ? MockBackendHttpsPort : MockBackendHttpPort;
                }

                TcpClient forwardedRequest = new();
                forwardedRequest.Connect(domain, port);

                // If we're getting a CONNECT request we're going to open a tunnel to the server and signal the client to start sending the encrypted data
                // We're going to act as a middleman and catch the data before sending it to the client (since we might want to redirect the request to the mock server)
                if (isConnectRequest)
                {
                    //See RFC 7230 section 3.1.2 for the status-line response format
                    //HTTP-version SP status-code SP reason-phrase CRLF
                    string responseLine = $"{httpVersion} 200 OK{LINE_SEPARATOR}{LINE_SEPARATOR}";
                    clientSocket.Send(responseLine.AsBytes(Encoding.UTF8));
                    NetworkStream clientStream = new(clientSocket);
                    HandleHTTPSForward(clientStream, forwardedRequest.GetStream(), domain);
                }
                else
                {
                    using Stream forwardedRequestStream = forwardedRequest.GetStream();
                    forwardedRequestStream.Write(rawClientRequestData.AsBytes(Encoding.UTF8));

                    Stopwatch timeoutStopWatch = new();
                    timeoutStopWatch.Start();

                    while (forwardedRequest.Available <= 0 && timeoutStopWatch.ElapsedMilliseconds < 60000)
                    {
                        Thread.Sleep(100);
                    }

                    if (timeoutStopWatch.ElapsedMilliseconds >= 60000 && clientSocket.Connected)
                    {
                        string responseLine = $"{httpVersion} 504 Gateway Timeout{LINE_SEPARATOR}{LINE_SEPARATOR}";
                        clientSocket.Send(responseLine.AsBytes(Encoding.UTF8));
                    }
                    else if (clientSocket.Connected)
                    {
                        Stream networkStream = new NetworkStream(clientSocket);
                        ForwardRequestToStream(forwardedRequestStream, networkStream);
                    }                    
                    timeoutStopWatch.Stop();
                }
                forwardedRequest.Close();
            }
            clientSocket.Close();
        }

        private static string SanitizeEndpointFromStartLine(string startLine)
        {
            //Sanitize the host string (HTTP start line will contains this, so if we have no matching header we'll have to sanitize the endpoint from the start line)
            if (startLine.Contains("://"))
            {
                startLine = startLine.Remove(0, startLine.IndexOf("://") + 3);
            }

            if (startLine.Contains("/"))
            {
                startLine = startLine.Remove(startLine.IndexOf("/"));
            }

            return startLine;
        }

        private static string GetClientResponse(Socket client)
        {
            byte[] buffer = new byte[256];
            string rawClientRequestData = "";
            while (client.Available > 0)
            {
                int gotBytes = client.Receive(buffer);
                rawClientRequestData += buffer.AsString(gotBytes, Encoding.UTF8);
            }
            return rawClientRequestData;
        }

        private static void HandleHTTPSForward(NetworkStream clientStream, NetworkStream serverStream, string requestEndpoint)
        {
            using SslStream sslClientStream = new(clientStream, true);
            sslClientStream.AuthenticateAsServer(new SslServerAuthenticationOptions()
            {
                AllowRenegotiation = true,
                ApplicationProtocols = new List<SslApplicationProtocol>() { SslApplicationProtocol.Http11, SslApplicationProtocol.Http2 },
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls11 | System.Security.Authentication.SslProtocols.Tls13,
                ServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate("certificate.p12"),
                RemoteCertificateValidationCallback = (a, b, c, d) => true
            });
            using SslStream sslServerStream = new(serverStream, true);
            sslServerStream.AuthenticateAsClient(requestEndpoint);
            ForwardRequestToStream(sslClientStream, sslServerStream);
            ForwardRequestToStream(sslServerStream, sslClientStream);
        }

        // TODO: We'll have to read the client's request to do path level matching and not only domain level matching
        private static string ReadToEnd(Stream stream)
        {
            if (stream.CanRead)
            {
                byte[] buffer = new byte[1024];
                int byteCount;
                string rawClientRequestData = "";

                while ((byteCount = stream.Read(buffer, 0, buffer.Length)) == buffer.Length)
                {
                    if (byteCount == buffer.Length)
                    {
                        rawClientRequestData += buffer.AsString();
                    }
                }
                rawClientRequestData += buffer.AsString(byteCount);

                return rawClientRequestData;
            }
            else
            {
                return "";
            }
        }

        private static void ForwardRequestToStream(Stream from, Stream to)
        {
            byte[] buffer;
            int bytesRead;
            string tmp = $"{LINE_SEPARATOR}{LINE_SEPARATOR}";
            if (!from.CanRead)
            {
                return;
            }

            do
            {
                buffer = new byte[256];
                bytesRead = from.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    tmp = buffer.AsString(bytesRead, Encoding.UTF8);
                    to.Write(buffer, 0, bytesRead);

                }
            } while (bytesRead > 0 && !tmp.EndsWith($"{LINE_SEPARATOR}{LINE_SEPARATOR}"));
            to.Flush();
        }
    }
}
