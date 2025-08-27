using DStreamDotnetTest;
using Katasec.DStream.Plugin;
using Katasec.DStream.Providers;
using Katasec.DStream.Providers.Input;
using Katasec.DStream.Providers.Output;

// Create and run the plugin host with strongly-typed configuration
var plugin = new GenericCounterPlugin();
var inputProvider = Katasec.DStream.Providers.ProviderRegistry.GetInputProvider("null");
var outputProvider = Katasec.DStream.Providers.ProviderRegistry.GetOutputProvider("console");

var host = Katasec.DStream.Plugin.PluginHostFactory.CreatePluginHost<GenericCounterPlugin, GenericCounterConfig>(
    plugin, inputProvider, outputProvider);
    
await host.RunAsync(args);
