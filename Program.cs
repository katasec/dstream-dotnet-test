// This is a .NET gRPC plugin for dstream that implements the HashiCorp go-plugin handshake protocol
// It outputs an infinite counter every 5 seconds and ensures proper handshake with dstream
using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using DStream.Plugin;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Collections.Generic;
using System.Text.Json;

namespace DStreamDotnetTest
{
    
    public class Program
    {
        public static bool IsStandalone { get; private set; } = false;
        
        static async Task Main(string[] args)
        {
            // Check if running in standalone mode
            IsStandalone = HashiCorpPluginUtils.IsStandaloneMode(args);
            
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
                int port = HashiCorpPluginUtils.FindAvailablePort();
                
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
                    // Send the HashiCorp go-plugin handshake string
                    HashiCorpPluginUtils.SendHandshakeString(port, logger);
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
    }
}
