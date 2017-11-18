using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;

namespace HotelApi
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services

            // Web API routes
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}",
                defaults: new { id = RouteParameter.Optional }
            );

            RunAsync().Wait();
        }
        


        static async Task RunAsync()
        {
            //ApiInterface.client.BaseAddress = new Uri("http://localhost:55268/");
            ApiInterface.client.DefaultRequestHeaders.Accept.Clear();
            ApiInterface.client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            
        }
    }
}
