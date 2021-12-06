using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Options;
using MockBackend_Core.Models.Collection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MockBackend_Core.EndpointCore
{
    public class CustomEndpointDataSource: MutableEndpointDataSource
    {
        public ObservableCollection<ControllerModel> Controllers { get; private set; }
        public CustomEndpointDataSource()
        {
            Controllers = new();
            Controllers.CollectionChanged += (sender, e) =>
            {
                SetEndpoints(MakeEndpoints(Controllers.ToList()));
            };
            SetEndpoints(MakeEndpoints(Controllers.ToList()));
            //SetEndpoints(MakeEndpoints(options.CurrentValue));
            //options.OnChange(config => SetEndpoints(MakeEndpoints(config)));
        }

        
        public void UpdateControllerCollection(List<ControllerModel> controllers)
        {
            Controllers = new ObservableCollection<ControllerModel>(controllers);
            Controllers.CollectionChanged += (sender, e) =>
            {
                SetEndpoints(MakeEndpoints(Controllers.ToList()));
            };
            SetEndpoints(MakeEndpoints(controllers));
        }

        private static IReadOnlyList<Endpoint> MakeEndpoints(List<ControllerModel> controllers)
        {
            return controllers.Select(controller => {
                /*
                    endpoints.MapGet("/hello/{name:alpha}", async context =>
                    {
                        var name = context.Request.RouteValues["name"];
                        await context.Response.WriteAsync($"Hello {name}!");
                    }); 
                 */
                RequestDelegate responseFunc = new(async (HttpContext context) =>
                {
                    if (controller.Headers.Count > 0)
                    {
                        foreach (var header in controller.Headers)
                        {
                            context.Response.Headers.Add(header.Key, header.Value);
                        }
                    }

                    if (controller.Delay > 0)
                    {
                        await Task.Delay(controller.Delay);
                    }

                    context.Response.StatusCode = controller.Status;
                    context.Response.ContentType = controller.ContentType;
                    if (controller.BodyFile != "")
                    {
                        if (!File.Exists(controller.BodyFile))
                        {
                            throw new Exception($"Cannot find specified file {controller.BodyFile}");
                        }

                        var fileStream = File.OpenRead(controller.BodyFile);
                        context.Response.ContentLength = fileStream.Length;
                        await context.Response.StartAsync();
                        var bodyWriter = context.Response.Body;

                        int bytesToRead = 10000;
                        byte[] buffer = new byte[bytesToRead];
                        int length;
                        do
                        {
                            if (!context.RequestAborted.IsCancellationRequested)
                            {

                                length = await fileStream.ReadAsync(buffer.AsMemory(0, bytesToRead));

                                await bodyWriter.WriteAsync(buffer.AsMemory(0, length));

                                await bodyWriter.FlushAsync();

                                buffer = new byte[bytesToRead];
                            }
                            else
                            {
                                length = -1;
                            }
                        } while (length > 0);

                        await context.Response.CompleteAsync();
                    }
                    else
                    {
                        await context.Response.WriteAsync(controller.Body);
                    }
                });
                return CreateEndpoint(controller.Method, controller.Path, responseFunc);
            })
            .ToList();
        }

        private static Endpoint CreateEndpoint(string method, string pattern, RequestDelegate requestDelegate) =>
            new RouteEndpointBuilder(
                requestDelegate: requestDelegate,
                routePattern: RoutePatternFactory.Parse(pattern),
                order: 0)
            {
                Metadata =
                {
                    // TODO: Save in a static list to avoid creating a new array for each path
                    new HttpMethodMetadata(new []{ method.ToUpper() })
                }
            }
            .Build();
    }
}
