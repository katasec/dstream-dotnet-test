// CounterPlugin.cs
// Sample plugin implementation that outputs a counter

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Katasec.DStream.Plugin;
using Katasec.DStream.Plugin.Interfaces;
using Katasec.DStream.Plugin.Models;
using Katasec.DStream.Proto;

namespace DStreamDotnetTest;

/// <summary>
/// A simple counter plugin that increments a counter and logs the value
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
    /// Process data using input and output providers
    /// </summary>
    public async Task ProcessAsync(IInput input, IOutput output, Dictionary<string, object> config, CancellationToken cancellationToken)
    {
        // Create a HashiCorp compatible logger
        var logger = new HCLogger(ModuleName);
        
        // Log startup message
        logger.Info(".NET Counter plugin started in ProcessAsync mode");
        logger.Info($"Using input provider: {input.GetType().Name}");
        logger.Info($"Using output provider: {output.GetType().Name}");
        
        // Extract interval from config if available
        int intervalMs = 5000; // Default to 5 seconds
        if (config.TryGetValue("interval", out var intervalObj) && intervalObj is string intervalStr)
        {
            if (int.TryParse(intervalStr, out int parsedInterval))
            {
                intervalMs = parsedInterval;
                logger.Info($"Using configured interval: {intervalMs}ms");
            }
        }
        
        // Run the counter loop, keeping the plugin alive until cancellation
        int counter = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            counter++;
            
            // Create a StreamItem with the counter value
            // Convert our counter object to JsonElement
            var counterJson = JsonSerializer.Serialize(new { counter = counter });
            var jsonElement = JsonDocument.Parse(counterJson).RootElement;
            
            // Create the StreamItem
            var item = StreamItem.Create(
                data: jsonElement,
                source: "counter-plugin",
                operation: "increment"
            );
            
            // Add metadata
            item.Metadata["timestamp"] = DateTime.UtcNow.ToString("o");
            
            // Write the counter to the output
            await output.WriteAsync(new[] { item }, cancellationToken);
            logger.Info($"Counter: {counter} written to output");

            // Wait for the configured interval or until cancellation
            try
            {
                await Task.Delay(intervalMs, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                logger.Info("Plugin received cancellation signal");
                break;
            }
        }
        
        logger.Info("Plugin stopped by cancellation");
    }
    
    /// <summary>
    /// Legacy execution method for backward compatibility
    /// </summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Create a HashiCorp compatible logger
        var logger = new HCLogger(ModuleName);
        
        // Log startup message
        logger.Info(".NET Counter plugin started in legacy mode");
        logger.Info("This method is deprecated. Please use the new input/output model.");
        
        // Run the counter loop, keeping the plugin alive until cancellation
        int counter = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            // Log counter value using HCLogger which writes to stderr
            // The HashiCorp go-plugin system will capture this and forward it to the host
            logger.Info($"Counter: {++counter}");

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
