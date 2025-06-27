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
using System.Text.Json;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace DStreamDotnetTest
{
    // HashiCorp compatible logger implementation that exactly matches the format expected by go-plugin
    public class HCLogger
    {
        private readonly string _name;
        private readonly TextWriter _writer;
        
        public HCLogger(string name, TextWriter writer = null)
        {
            _name = name;
            _writer = writer ?? Console.Error; // HashiCorp go-plugin uses stderr for logging
        }
        
        public void Log(string level, string message, params object[] args)
        {
            try
            {
                // Format the message with args if any
                string formattedMessage = args.Length > 0 ? string.Format(message, args) : message;
                
                // Create the log entry in HashiCorp hclog format
                // The format must exactly match what HashiCorp's go-plugin expects
                var logEntry = new Dictionary<string, object>
                {
                    ["@level"] = level,
                    ["@message"] = formattedMessage,
                    ["@module"] = _name
                };
                
                // Serialize to JSON and write to stderr
                string json = JsonSerializer.Serialize(logEntry);
                _writer.WriteLine(json);
                _writer.Flush();
            }
            catch (Exception ex)
            {
                // If logging fails, write a simple error message to stderr
                // This shouldn't happen in normal operation
                try
                {
                    _writer.WriteLine($"{{\"@level\":\"error\",\"@message\":\"Logging error: {ex.Message}\",\"@module\":\"{_name}\"}}");
                    _writer.Flush();
                }
                catch
                {
                    // Last resort - if we can't even log the error, just ignore it
                }
            }
        }
        
        public void Debug(string message, params object[] args) => Log("debug", message, args);
        public void Info(string message, params object[] args) => Log("info", message, args);
        public void Warn(string message, params object[] args) => Log("warn", message, args);
        public void Error(string message, params object[] args) => Log("error", message, args);
    }
    
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

        // Implement the Start method from the PluginBase class
        public override async Task<Empty> Start(Struct request, ServerCallContext context)
        {
            // Create a HashiCorp compatible logger
            var logger = new HCLogger("dotnet-counter");
            
            try
            {
                // Log startup message
                logger.Info(".NET Counter plugin started");
                
                // Run the counter loop in the current task, keeping the plugin alive
                // This is important - the plugin should block until cancellation
                int counter = 0;
                while (!context.CancellationToken.IsCancellationRequested)
                {
                    counter++;
                    
                    // Log counter value using HCLogger which writes to stderr
                    // The HashiCorp go-plugin system will capture this and forward it to the host
                    logger.Info($"Counter: {counter}");
                    
                    // Only print to console if in standalone mode
                    if (Program.IsStandalone)
                    {
                        Console.WriteLine($"Counter: {counter}");
                    }
                    
                    // Wait for 5 seconds or until cancellation
                    try
                    {
                        await Task.Delay(5000, context.CancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        logger.Info("Plugin received cancellation signal");
                        break;
                    }
                }
                
                logger.Info("Plugin stopped by cancellation");
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation occurs
                logger.Info("Plugin stopped by cancellation");
            }
            catch (Exception ex)
            {
                // Log any unexpected exceptions
                logger.Error("Error: {0}", ex.Message);
            }
            
            // Return an empty response when done
            return new Empty();
        }
    }
    
    public class Program
    {
        public static bool IsStandalone { get; private set; } = false;
        
        static async Task Main(string[] args)
        {
            // Check if running in standalone mode
            IsStandalone = args.Length > 0 && args[0] == "--standalone";
            
            // Create a HashiCorp compatible logger
            var logger = new HCLogger("dotnet-plugin");
            
            try
            {
                // In plugin mode, only redirect stdout, leaving stderr available for logging
                if (!IsStandalone)
                {
                    // Only redirect stdout, not stderr (which is used for logging)
                    Console.SetOut(TextWriter.Null);
                }
                
                // Find an available port
                int port = FindAvailablePort();
                
                // Create and start the gRPC server
                var builder = WebApplication.CreateBuilder(args);
                
                // Add services to the container
                builder.Services.AddGrpc();
                
                // Configure Kestrel to use the specific port
                builder.WebHost.ConfigureKestrel(options =>
                {
                    options.ListenLocalhost(port, listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http2;
                    });
                });
                
                // Suppress ASP.NET Core logging in plugin mode
                if (!IsStandalone)
                {
                    builder.Logging.ClearProviders();
                }
                
                var app = builder.Build();
                
                // Configure the HTTP request pipeline
                app.MapGrpcService<PluginService>();
                
                // Start the server
                await app.StartAsync();
                
                // Log server startup
                logger.Info("gRPC server started on port {0}", port);
                
                // Output the handshake string after the server has started
                if (!IsStandalone)
                {
                    // Output the handshake string to stdout
                    // We need to temporarily restore stdout for this
                    var originalOut = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                    Console.SetOut(originalOut);
                    Console.WriteLine($"1|1|tcp|127.0.0.1:{port}|grpc");
                    Console.SetOut(TextWriter.Null); // Suppress further stdout output
                    
                    logger.Info("Handshake string sent to host");
                }
                else
                {
                    Console.WriteLine($"Server started on port {port} in standalone mode");
                    Console.WriteLine("Press Ctrl+C to stop the server");
                }
                
                // Create a reset event for graceful shutdown
                var exitEvent = new ManualResetEvent(false);
                
                // Register for SIGINT (Ctrl+C) and SIGTERM
                Console.CancelKeyPress += (sender, eventArgs) => {
                    logger.Info("Received shutdown signal");
                    // Cancel the default behavior (termination)
                    eventArgs.Cancel = true;
                    // Signal the exit event
                    exitEvent.Set();
                };
                
                // Wait for exit signal
                exitEvent.WaitOne();
                
                // Gracefully stop the server
                logger.Info("Shutting down server");
                await app.StopAsync();
            }
            catch (Exception ex)
            {
                logger.Error("Fatal error: {0}", ex.Message);
                if (IsStandalone)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
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
    }
}
