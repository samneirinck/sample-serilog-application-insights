using System;
using System.Diagnostics;
using System.Net.Http;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.ExtensionMethods;

namespace TestSomeThings
{
    public class Program
    {
        internal const string ApplicationInsightsKey = "FILLME";
        private static int Main(string[] args)
        {


            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: "{Message:lj}{Properties}{NewLine}")
                .WriteTo.ApplicationInsights(ApplicationInsightsKey, LogEventToTelemetryConverter)
                .CreateLogger();

            try
            {
                Log.Information("Starting web host");

                var host = WebHost.CreateDefaultBuilder(args)
                    .UseStartup<Startup>()
                    .UseSerilog()
                    .Build();

                host.Run();

                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static ITelemetry LogEventToTelemetryConverter(LogEvent logEvent, IFormatProvider formatProvider)
        {
            ITelemetry telemetry = null;
            if ((logEvent.Level == LogEventLevel.Error || logEvent.Level == LogEventLevel.Fatal) &&
                logEvent.Exception != null)
            {
                telemetry = logEvent.ToDefaultExceptionTelemetry(formatProvider, true, true, false);
            }
            else
            {
                telemetry = logEvent.ToDefaultTraceTelemetry(
                    formatProvider,
                    includeLogLevelAsProperty: true,
                    includeRenderedMessageAsProperty: true,
                    includeMessageTemplateAsProperty: false);
            }

            var activity = Activity.Current;
            if (activity != null)
            {
                telemetry.Context.Operation.ParentId = activity.ParentId;
                telemetry.Context.Operation.Id = activity.RootId ?? activity.Id;
                telemetry.Context.Cloud.RoleName = "Sample program";
            }

            return telemetry;
        }
    }

    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddApplicationInsightsTelemetry(Program.ApplicationInsightsKey);
            services.AddMvc();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            
            app.UseMvc();

            app.Run(async (context) =>
            {
                Log.Warning("Doing some things");
                await context.Response.WriteAsync("Hello World!");
                var c = new HttpClient();

                await c.GetStringAsync("http://www.hln.be");
                Log.Error(new NullReferenceException("uhoh"), "This is wrong");
            });
        }
    }
}
