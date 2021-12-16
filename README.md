# MockBackend-Core
Mock RESTful backend CLI

## Usage
### CLI parameters
```
 -c/--collection <path> => path the JSON file that should be used

 -p/--port <port> => Port to use for HTTP (Overrides the collection specified port)

 -sp/--httpsport <port> => Port to use for HTTPS (Overrides the collection specified port)

```

### Collection JSON model

```
{
  "Name": string, // Default value: "Unamed collection"
  "EndPoint": string, // Endpoint to listen to. Default value: "*" (All)
  "HttpPort": integer, // HTTP port to listen on. If omitted the application will not listen on HTTP. Default value: 0 (0 is the same as omitting the field)
  "HttpsPort": integer, // HTTPS port to listen on. If omitted the application will not listen on HTTPS. Default value: 0 (0 is the same as omitting the field)
  "ProxyServerEndpoint": string, // IP for the proxyserver to listen to. Default value: "127.0.0.1"
  "ProxyServerPort": integer, // Port the proxy server listens to. If omitted the proxy server will not start. Default value: 0 (0 is the same as omitting the field)
  "DomainsToRelay": [
    string // Domains to relay to the mock backend, the proxy will act as a passthrough proxy for all other requests
  ],
  "Controllers": [ // Array of custom paths to respond to. Any other request will return 404 or 405. Default: []
    {
      "Path": string, // The rest path this path responds to. Required
      "Method": string, // HTTP Method. Can be any of GET, POST, PUT or DELETE. Required
      "Status": integer, // HTTP status to respond with. Default value: 200
      "ContentType": string, //The content type of the response. Default value: "application/text"
      "Body": string, // The contents of the response body. Default value: ""
      "BodyFile": string, // Alternative to the Body variable. Loads the contents from the specified file. Ignored if empty or omitted. Takes priority over the Body variable. Default: ""
      "Delay": integer //Minimum delay in milliseconds before the response is returned. Default value 0
    }
  ]
}
```
