// CounterPlugin.cs
// Sample plugin implementation that outputs a counter

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DStream.Plugin;

namespace DStreamDotnetTest
{
    /// <summary>
    /// Sample plugin implementation that outputs a counter
    /// </summary>
    public class CounterPlugin : IDStreamPlugin
    {
        /// <summary>
        /// Gets the name of the plugin module for logging
        /// </summary>
        public string ModuleName => "dotnet-counter";
        
        /// <summary>
        /// Gets the schema fields for the plugin
        /// </summary>
        public IEnumerable<FieldSchema> GetSchemaFields()
        {
            yield return new FieldSchema
            {
                Name = "interval",
                Type = "string",
                Description = "Interval between counter increments"
            };
        }
        
        /// <summary>
        /// The main execution method for the plugin
        /// </summary>
        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // Create a HashiCorp compatible logger
            var logger = new HCLogger(ModuleName);
            
            // Log startup message
            logger.Info(".NET Counter plugin started");
            
            // Run the counter loop, keeping the plugin alive until cancellation
            int counter = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                counter++;
                
                // Log counter value using HCLogger which writes to stderr
                // The HashiCorp go-plugin system will capture this and forward it to the host
                logger.Info($"Counter: {counter}");
                
                // Only print to console if in standalone mode
                if (DStreamPluginHost<CounterPlugin>.IsStandalone)
                {
                    Console.WriteLine($"Counter: {counter}");
                }
                
                // Wait for 5 seconds or until cancellation
                try
                {
                    await Task.Delay(5000, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    logger.Info("Plugin received cancellation signal");
                    break;
                }
            }
            
            logger.Info("Plugin stopped by cancellation");
        }
    }
}
