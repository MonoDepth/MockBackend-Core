using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using MockBackend_Core.Models;
using MockBackend_Core.Models.Collection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using System.Threading;
using MockBackend_Core.Extensions;
using MockBackend_Core.EndpointCore;

namespace MockBackend_Core
{
    public class Program
    {
        static CustomEndpointDataSource DataSource { get; set; } = new CustomEndpointDataSource();
        public static void Main(string[] args)
        {
            var startupModel = CreateStartupArgModel(SanitizeArgs(args));            
            if (startupModel.CollectionPath == null || startupModel.CollectionPath == "")
            {
                throw new Exception("Please provide a path to the collection JSON using the -c or --collection parameter");
            }

            if (!File.Exists(startupModel.CollectionPath))
            {
                throw new Exception($"Cannot find the path {startupModel.CollectionPath}");
            }

            string json = File.ReadAllText(startupModel.CollectionPath);
            CollectionModel collectionModel = JsonSerializer.Deserialize<CollectionModel>(json) ?? throw new Exception($"Failed to parse {startupModel.CollectionPath}");
            Console.WriteLine($"Using collection {collectionModel.Name}");
            CancellationTokenSource cancellationToken = new();
            IHost host = CreateHostBuilder(startupModel, collectionModel).Build();
            Task hostTask = host.RunAsync(cancellationToken.Token);

            string command;
            while ((command = Console.ReadLine()?.ToUpper() ?? "") != "Q") {
                switch (command)
                {
                    //TODO: Hot reloading of paths etc
                    case "RELOAD":
                        cancellationToken.Cancel();
                        Task.WaitAll(hostTask);
                        cancellationToken = new();
                        json = File.ReadAllText(startupModel.CollectionPath);
                        collectionModel = JsonSerializer.Deserialize<CollectionModel>(json) ?? throw new Exception($"Failed to parse {startupModel.CollectionPath}");
                        hostTask = CreateHostBuilder(startupModel, collectionModel).Build().RunAsync(cancellationToken.Token);
                        break;
                    case "REFRESH":
                        json = File.ReadAllText(startupModel.CollectionPath);
                        collectionModel = JsonSerializer.Deserialize<CollectionModel>(json) ?? throw new Exception($"Failed to parse {startupModel.CollectionPath}");
                        DataSource.UpdateControllerCollection(collectionModel.Controllers);                        
                        break;
                    case "TESTADD":
                        DataSource.Controllers.Add(new ControllerModel
                        {
                            Body = "TEST ADD",
                            Method = "GET",
                            Status = 200,
                            Path = "testadd"
                        });
                        break;
                }
            }
        }

        public static IHostBuilder CreateHostBuilder(StartupArgsModel startupModel, CollectionModel collection) {
            var urls = new List<string>();
            DataSource.UpdateControllerCollection(collection.Controllers);
            int httpPort = startupModel.HttpPort ?? collection.HttpPort;
            int httpsPort = startupModel.HttpsPort ?? collection.HttpsPort;

            if (httpPort > 0)
            {
                urls.Add($"http://{collection.EndPoint}:{httpPort}");
            }
            if (httpsPort > 0)
            {
                urls.Add($"https://{collection.EndPoint}:{httpsPort}");
            }

            return Host.CreateDefaultBuilder()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls(urls.ToArray());
                    webBuilder.UseStartup((x) =>
                    {
                        return new Startup(x.Configuration, collection.Controllers);
                    });
                })
                .ConfigureServices(services => 
                {
                    services.AddCustomEndpoints(DataSource);
                });
        }

        public static Dictionary<string, string> SanitizeArgs(string[] args)
        {
            /* return args.Select(arg =>
             {
                 if (arg.StartsWith("--"))
                 {
                     return arg[2..].ToUpper();
                 }
                 else if (arg.StartsWith("-") || arg.StartsWith("/"))
                 {
                     return arg[1..].ToUpper();
                 }
                 else
                 {
                     return arg.ToUpper();
                 }
             }).ToArray();
            */
            //List<KeyValuePair<string, string>> argMap = new();
            Dictionary<string, string> argMap = new();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                string value = "";

                if (i + 1 < args.Length && !(args[i+1].StartsWith("-") || args[i + 1].StartsWith("/")))
                {
                    value = args[i + 1];
                    i++;
                }


                if (arg.StartsWith("--"))
                {                    
                    argMap.Add(arg[2..].ToUpper(), value);
                }
                else if (arg.StartsWith("-") || arg.StartsWith("/"))
                {
                    argMap.Add(arg[1..].ToUpper(), value);
                }
            }
            return argMap;
        }

        public static StartupArgsModel CreateStartupArgModel(Dictionary<string, string> sanitizedArgs)
        {
            StartupArgsModel model = new();

            foreach (var arg in sanitizedArgs)
            {
                switch(arg.Key)
                {
                    case "H":
                    case "HELP":
                        break;

                    case "P":
                    case "PORT":
                        model.HttpPort = int.Parse(arg.Value);
                        break;

                    case "SP":
                    case "HTTPSPORT":
                        model.HttpsPort = int.Parse(arg.Value);
                        break;

                    case "C":
                    case "COLLECTION":
                        model.CollectionPath = arg.Value;
                        break;
                }
            }

            return model;
        }
    }
}
