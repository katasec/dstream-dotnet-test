// CounterPlugin.cs
// Sample plugin implementation that outputs a counter

using System.Text.Json;
using Katasec.DStream.Plugin;
using Katasec.DStream.Plugin.Interfaces;
using Katasec.DStream.Plugin.Models;
using Katasec.DStream.Proto;

namespace DStreamDotnetTest;

/// <summary>
/// Strongly typed configuration for the CounterPlugin
/// </summary>
public class CounterPluginConfig
{
    public GlobalConfig Config { get; set; } = new GlobalConfig();
    public InputConfig Input { get; set; } = new InputConfig();
    public OutputConfig Output { get; set; } = new OutputConfig();
    
    // Helper method to create a CounterPluginConfig from a raw config dictionary
    public static CounterPluginConfig FromDictionary(Dictionary<string, object> config, IInput input, IOutput output)
    {
        var result = new CounterPluginConfig
        {
            Input = new InputConfig { Provider = input.Name },
            Output = new OutputConfig { Provider = output.Name }
        };
        
        // Extract interval from config if available
        if (config.TryGetValue("interval", out var intervalObj))
        {
            result.Config.Interval = ExtractNumberValue(intervalObj, 5000);
        }
        
        // For console output, set format
        if (output.Name == "console")
        {
            result.Output.Config.Format = "json";
        }
        
        return result;
    }
    
    // Helper method to extract a number value from a protobuf Value object
    public static int ExtractNumberValue(object valueObj, int defaultValue)
    {
        if (valueObj == null) return defaultValue;
        
        // If it's a dictionary with NumberValue property
        if (valueObj is Dictionary<string, object> dict)
        {
            if (dict.TryGetValue("NumberValue", out var numValue) && numValue != null)
            {
                if (numValue is double doubleValue)
                {
                    return (int)doubleValue;
                }
                else if (int.TryParse(numValue.ToString(), out int intValue))
                {
                    return intValue;
                }
            }
        }
        // If it's a direct number
        else if (valueObj is int intValue)
        {
            return intValue;
        }
        else if (valueObj is double doubleValue)
        {
            return (int)doubleValue;
        }
        // If it's a string that can be parsed as a number
        else if (valueObj is string strValue && int.TryParse(strValue, out int parsedValue))
        {
            return parsedValue;
        }
        
        return defaultValue;
    }
}

/// <summary>
/// Global configuration for the CounterPlugin
/// </summary>
public class GlobalConfig
{
    public int Interval { get; set; } = 5000; // Default to 5 seconds
}

/// <summary>
/// Input configuration for the CounterPlugin
/// </summary>
public class InputConfig
{
    public string Provider { get; set; } = "null";
    public Dictionary<string, object> Config { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Output configuration for the CounterPlugin
/// </summary>
public class OutputConfig
{
    public string Provider { get; set; } = "console";
    public OutputDetails Config { get; set; } = new OutputDetails();
}

/// <summary>
/// Output configuration details
/// </summary>
public class OutputDetails
{
    public string Format { get; set; } = "json";
}

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
        
        // Log the raw configuration received from dstream CLI
        logger.Info("Raw configuration structure:");
        var configJson = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        logger.Info(configJson);
        
        // Create a strongly typed configuration object using our helper method
        int intervalMs = 5000; // Default to 5 seconds
        
        try
        {
            // Use our helper method to create a strongly typed configuration
            var pluginConfig = CounterPluginConfig.FromDictionary(config, input, output);
            
            logger.Info("Created strongly typed configuration:");
            logger.Info($"  Interval: {pluginConfig.Config.Interval}ms");
            logger.Info($"  Input Provider: {pluginConfig.Input.Provider}");
            logger.Info($"  Output Provider: {pluginConfig.Output.Provider}");
            if (output.Name == "console")
            {
                logger.Info($"  Output Format: {pluginConfig.Output.Config.Format}");
            }
            
            // Use the strongly typed configuration
            intervalMs = pluginConfig.Config.Interval;
        }
        catch (Exception ex)
        {
            logger.Error($"Error creating strongly typed configuration: {ex.Message}");
            
            // Fall back to the old way
            logger.Info("Falling back to direct dictionary access");
            if (config.TryGetValue("interval", out var intervalObj))
            {
                // Try to extract the interval value using our helper method
                intervalMs = CounterPluginConfig.ExtractNumberValue(intervalObj, intervalMs);
            }
        }
        
        logger.Info($"Using configured interval: {intervalMs}ms");
        
        // Run the counter loop, keeping the plugin alive until cancellation
        int counter = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            // Create a StreamItem with the counter value
            // Convert our counter object to JsonElement
            var counterJson = JsonSerializer.Serialize(new { counter = ++counter });
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
    
}
