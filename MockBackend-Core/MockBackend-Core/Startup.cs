using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MockBackend_Core.Models.Collection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MockBackend_Core
{
    public class Startup
    {
        List<ControllerModel> controllers;
        public Startup(List<ControllerModel> controllers)
        {
            this.controllers = controllers;
        }
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                controllers.ForEach(controller =>
                {
                    RequestDelegate responseFunc = new (async (HttpContext context) =>
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

                    switch (controller.Method.ToUpper())
                    {
                        case "GET":
                            endpoints.MapGet(controller.Path, responseFunc);
                            break;
                        case "POST":
                            endpoints.MapPost(controller.Path, responseFunc);
                            break;
                        case "PUT":
                            endpoints.MapPut(controller.Path, responseFunc);
                            break;
                        case "DELETE":
                            endpoints.MapDelete(controller.Path, responseFunc);
                            break;
                    }
                });
            });
        }
    }
}
