using System;
using System.Threading.Tasks;
using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using DStream.Plugin;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Linq;
using Microsoft.AspNetCore.Hosting.Server.Features;
using System.IO;

namespace DStreamDotnetTest
{
    // Implement the Plugin service
    public class PluginService : DStream.Plugin.Plugin.PluginBase
    {
        private readonly ILogger<PluginService> _logger;

        public PluginService(ILogger<PluginService> logger)
        {
            _logger = logger;
        }

        public override Task<GetSchemaResponse> GetSchema(Empty request, ServerCallContext context)
        {
            _logger.LogInformation("GetSchema called");
            
            // Create a simple schema
            var response = new GetSchemaResponse();
            response.Fields.Add(new FieldSchema { 
                Name = "interval", 
                Type = "string", 
                Description = "Interval between counter increments" 
            });
            
            return Task.FromResult(response);
        }

        public override Task<Empty> Start(Struct request, ServerCallContext context)
        {
            _logger.LogInformation("Start called with parameters: {Parameters}", request);
            
            // Start a background task to output numbers
            Task.Run(async () => {
                int counter = 0;
                while (true)
                {
                    counter++;
                    Console.WriteLine($"Counter: {counter}");
                    await Task.Delay(5000); // Wait for 5 seconds
                }
            });
            
            // Return an empty response
            return Task.FromResult(new Empty());
        }
    }
    
    public class Program
    {
        static int counter = 0;
        
        public static void Main(string[] args)
        {
            // Check if we're being run by the go-plugin system
            bool isPlugin = Environment.GetEnvironmentVariable("PLUGIN_PROTOCOL_VERSIONS") != null;
            
            if (isPlugin)
            {
                // When running as a plugin, we need to:                
                // 1. Start a gRPC server on a fixed port
                // 2. Output the handshake string as the very first output
                // 3. Suppress all other console output
                
                // Use a fixed port for the gRPC server
                int port = 50051;
                
                // Immediately output the handshake string as the first line
                // Format: 1|1|tcp|localhost:PORT|grpc
                Console.WriteLine($"1|1|tcp|localhost:{port}|grpc");
                
                // Redirect Console.Out to TextWriter.Null to suppress all further console output
                Console.SetOut(TextWriter.Null);
                
                // Create a host builder with logging disabled
                var hostBuilder = Host.CreateDefaultBuilder(args)
                    .ConfigureLogging(logging => {
                        logging.ClearProviders(); // Remove all logging providers
                    })
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.ConfigureKestrel(options =>
                        {
                            options.ListenLocalhost(port, listenOptions =>
                            {
                                listenOptions.Protocols = HttpProtocols.Http2;
                            });
                        });
                        webBuilder.UseStartup<Startup>();
                    });
                
                // Build and run the host
                var host = hostBuilder.Build();
                
                // Start a background task to increment the counter (no output)
                Task.Run(async () => {
                    while (true)
                    {
                        counter++;
                        await Task.Delay(5000); // Wait for 5 seconds
                    }
                });
                
                // Run the host
                host.Run();
            }
            else
            {
                // Running in standalone mode
                var host = CreateHostBuilder(args).Build();
                
                // Start a background task to output numbers
                Task.Run(async () => {
                    while (true)
                    {
                        counter++;
                        Console.WriteLine($"Counter: {counter}");
                        await Task.Delay(5000); // Wait for 5 seconds
                    }
                });
                
                // Run the host
                host.Run();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(options =>
                    {
                        // Use a random port when running as a plugin, otherwise use port 50051
                        if (Environment.GetEnvironmentVariable("PLUGIN_PROTOCOL_VERSIONS") != null)
                        {
                            // When running as a plugin, use a dynamic port
                            options.ListenLocalhost(0, listenOptions =>
                            {
                                listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
                            });
                        }
                        else
                        {
                            // When running standalone, use port 50051
                            options.ListenLocalhost(50051, listenOptions =>
                            {
                                listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
                            });
                        }
                    });
                    webBuilder.UseStartup<Startup>();
                });
    }

    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddGrpc();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<PluginService>();
            });
        }
    }
    
    // Simple console logger for standalone mode
    public class ConsoleLogger<T> : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            // Don't log to console when running as a plugin to avoid interfering with the protocol
            if (Environment.GetEnvironmentVariable("PLUGIN_PROTOCOL_VERSIONS") == null)
            {
                Console.WriteLine($"[{logLevel}] {formatter(state, exception)}");
            }
        }
    }
}
