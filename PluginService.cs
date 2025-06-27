// PluginService.cs
// Implementation of the Plugin service for dstream

using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using DStream.Plugin;

namespace DStreamDotnetTest
{
    /// <summary>
    /// Implementation of the Plugin service for dstream
    /// </summary>
    public class PluginService : DStream.Plugin.Plugin.PluginBase
    {
        private static int counter = 0;
        
        /// <summary>
        /// Gets the schema for the plugin
        /// </summary>
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

        /// <summary>
        /// Starts the plugin and runs the counter loop
        /// </summary>
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
}
