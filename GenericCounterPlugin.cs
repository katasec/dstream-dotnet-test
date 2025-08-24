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
        // Create a logger and log the configuration
        var logger = new HCLogger(ModuleName);
        logger.Info($"Hello World plugin started with interval: {config.Interval}ms");
        
        // Simple counter loop
        int counter = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            // Increment counter and create a simple message
            counter++;
            string message = $"Count: {counter}";
            logger.Info(message);
            
            // Create a simple stream item with the counter value
            var data = JsonDocument.Parse($"{{\"counter\": {counter}}}").RootElement;
            var item = StreamItem.Create(data, "hello-world", "increment");
            
            // Write to output and wait for the next interval
            await output.WriteAsync(new[] { item }, cancellationToken);
            
            try { await Task.Delay(config.Interval, cancellationToken); }
            catch (TaskCanceledException) { break; }
        }
    }
}
