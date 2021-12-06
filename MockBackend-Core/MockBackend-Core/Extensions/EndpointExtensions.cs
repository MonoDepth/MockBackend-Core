using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using MockBackend_Core.EndpointCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using MockBackend_Core.Models.Collection;

namespace MockBackend_Core.Extensions
{
    public static class EndpointExtensions
    {
        //https://mariusgundersen.net/dynamic-endpoint-routing/
        public static void AddCustomEndpoints(this IServiceCollection services, CustomEndpointDataSource dataSource)
        {
            services.AddSingleton(dataSource);
        }

        public static void UseCustomEndpoints(this IApplicationBuilder builder, Action<IEndpointRouteBuilder>? configure = null)
        {
            builder.UseEndpoints(endpoints => { 
                var dataSource = endpoints.ServiceProvider.GetService<CustomEndpointDataSource>();

                if (dataSource is null)
                {
                    throw new Exception("datasource is null! (Did you forget to call Services.AddCustomEndpoints?)");
                }

                endpoints.DataSources.Add(dataSource);
                configure?.Invoke(endpoints);
            });
        }

        public static void MapControllerModel(this IEndpointRouteBuilder endpoints, ControllerModel controller)
        {
            var dataSource = endpoints.ServiceProvider.GetService<CustomEndpointDataSource>();
        }
    }
}
