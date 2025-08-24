using DStreamDotnetTest;
using DStreamDotnetTest.Providers;
using Katasec.DStream.Plugin;

// Register providers
ProviderRegistry.RegisterInputProvider<NullInputProvider>("null");
ProviderRegistry.RegisterOutputProvider<ConsoleOutputProvider>("console");

// Create and run the plugin host with strongly-typed configuration
var host = new DStreamPluginHost<GenericCounterPlugin, GenericCounterConfig>();
await host.RunAsync(args);
