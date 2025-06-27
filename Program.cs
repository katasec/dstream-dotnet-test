// This is a .NET gRPC plugin for dstream that implements the HashiCorp go-plugin handshake protocol
// It outputs an infinite counter every 5 seconds and ensures proper handshake with dstream
using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using DStream.Plugin;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace DStreamDotnetTest
{
    // Implement the Plugin service
    public class PluginService : DStream.Plugin.Plugin.PluginBase
    {
        private static int counter = 0;
        
        public override Task<GetSchemaResponse> GetSchema(Empty request, ServerCallContext context)
        {
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
            // Start a background task to output numbers
            Task.Run(async () => {
                while (true)
                {
                    counter++;
                    // Only print to console if in standalone mode
                    if (Program.IsStandalone)
                    {
                        Console.WriteLine($"Counter: {counter}");
                    }
                    await Task.Delay(5000); // Wait for 5 seconds
                }
            });
            
            // Return an empty response
            return Task.FromResult(new Empty());
        }
    }
    
    public class Program
    {
        public static bool IsStandalone { get; private set; } = false;
        private static int counter = 0;
        private static TaskCompletionSource<bool> _serverStarted = new TaskCompletionSource<bool>();
        
        public static void Main(string[] args)
        {
            // Create a reset event for graceful shutdown
            var exitEvent = new ManualResetEvent(false);
            
            // Register for SIGINT (Ctrl+C) and SIGTERM
            Console.CancelKeyPress += (sender, eventArgs) => {
                // Cancel the default behavior (termination)
                eventArgs.Cancel = true;
                // Signal the exit event
                exitEvent.Set();
            };
            
            // Check if we're running in standalone mode (explicit flag needed)
            IsStandalone = args.Length > 0 && args[0] == "--standalone";
            
            // Find an available port
            int port = FindAvailablePort();
            
            if (!IsStandalone)
            {
                // Default mode is plugin mode
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
                
                // Build the host
                var host = hostBuilder.Build();
                
                // Start the host in a separate task
                Task.Run(async () => {
                    // Start the host
                    await host.StartAsync();
                    
                    // Signal that the server has started
                    _serverStarted.SetResult(true);
                    
                    // Wait for the host to stop
                    await host.WaitForShutdownAsync();
                });
                
                // Wait for the server to start
                _serverStarted.Task.Wait();
                
                // Output the handshake string as the first line AFTER the server has started
                // Format: 1|1|tcp|localhost:PORT|grpc
                Console.WriteLine($"1|1|tcp|localhost:{port}|grpc");
                
                // Redirect Console.Out to TextWriter.Null to suppress all further console output
                Console.SetOut(TextWriter.Null);
                
                // Wait for exit signal
                exitEvent.WaitOne();
                
                // Gracefully stop the host when exit is signaled
                host.StopAsync().Wait();
                host.Dispose();
            }
            else
            {
                // Running in standalone mode (explicitly requested)
                Console.WriteLine("Running in standalone mode...");
                var host = CreateHostBuilder(args, port).Build();
                
                // Start a background task to output numbers
                Task.Run(async () => {
                    while (true)
                    {
                        Console.WriteLine($"Counter: {counter}");
                        await Task.Delay(5000); // Wait for 5 seconds
                    }
                });
                
                // Run the host
                host.Run();
            }
        }

        // Find an available port by creating a temporary socket
        private static int FindAvailablePort()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                return ((IPEndPoint)socket.LocalEndPoint).Port;
            }
        }
        
        public static IHostBuilder CreateHostBuilder(string[] args, int port) =>
            Host.CreateDefaultBuilder(args)
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
    }
    
    // Configure the ASP.NET Core application
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddGrpc();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<PluginService>();
            });
        }
    }
}
