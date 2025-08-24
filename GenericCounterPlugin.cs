// GenericCounterPlugin.cs
// Sample plugin implementation that uses the generic interface with strongly-typed configuration

using System.Text.Json;
using Katasec.DStream.Plugin;
using Katasec.DStream.Plugin.Interfaces;
using Katasec.DStream.Plugin.Models;
using Katasec.DStream.Proto;
using HCLog.Net;
namespace DStreamDotnetTest;

/// <summary>
/// Strongly typed configuration for the GenericCounterPlugin
/// </summary>
public class GenericCounterConfig
{
    /// <summary>
    /// Interval between counter increments in milliseconds
    /// </summary>
    public int Interval { get; set; } = 5000;
    
    // We're keeping only the interval property to match the existing dotnet-counter task
    // Other properties can be added later as the configuration evolves
}

/// <summary>
/// A simple counter plugin that uses the generic interface with strongly-typed configuration
/// </summary>
public class GenericCounterPlugin : IDStreamPlugin<GenericCounterConfig>
{
    /// <summary>
    /// Gets the name of the plugin module for logging
    /// </summary>
    public string ModuleName => "generic-counter";
    
    /// <summary>
    /// Gets the schema fields for the plugin
    /// </summary>
    public IEnumerable<FieldSchema> GetSchemaFields()
    {
        yield return new FieldSchema
        {
            Name = "interval",
            Type = "number",
            Description = "Interval between counter increments in milliseconds"
        };
    }
    
    /// <summary>
    /// Process data using input and output providers with strongly-typed configuration
    /// </summary>
    public async Task ProcessAsync(IInput input, IOutput output, GenericCounterConfig config, CancellationToken cancellationToken)
    {
        // Create a HashiCorp compatible logger
        var logger = new HCLogger(ModuleName);
        
        // Log startup message
        logger.Info("Generic Counter plugin started with strongly-typed configuration");
        logger.Info($"Using input provider: {input.GetType().Name}");
        logger.Info($"Using output provider: {output.GetType().Name}");
        
        // Log the strongly-typed configuration
        logger.Info("Strongly-typed configuration:");
        logger.Info($"  Interval: {config.Interval}ms");
        
        // Run the counter loop, keeping the plugin alive until cancellation
        int counter = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            // Create the counter data
            var counterData = new Dictionary<string, object>
            {
                ["counter"] = ++counter,
                ["prefix"] = "Count" // Using a default prefix since we removed it from config
            };
            
            // Always include timestamp
            counterData["timestamp"] = DateTime.UtcNow.ToString("o");
            
            // Format the output message
            string message = $"Count: {counter}";
            
            // Convert our counter object to JsonElement
            var counterJson = JsonSerializer.Serialize(counterData);
            var jsonElement = JsonDocument.Parse(counterJson).RootElement;
            
            // Create the StreamItem
            var item = StreamItem.Create(
                data: jsonElement,
                source: "generic-counter-plugin",
                operation: "increment"
            );
            
            // Add metadata
            item.Metadata["plugin_type"] = "generic";
            
            // Write the counter to the output
            await output.WriteAsync(new[] { item }, cancellationToken);
            logger.Info($"{message} written to output");

            // Wait for the configured interval or until cancellation
            try
            {
                await Task.Delay(config.Interval, cancellationToken);
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
