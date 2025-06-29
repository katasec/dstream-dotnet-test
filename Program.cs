using DStreamDotnetTest;
using DStreamDotnetTest.Providers;
using Katasec.DStream.Plugin;

// Register providers
ProviderRegistry.RegisterInputProvider<NullInputProvider>("null");
ProviderRegistry.RegisterOutputProvider<ConsoleOutputProvider>("console");

// Create and run the plugin host
var host = new DStreamPluginHost<CounterPlugin>();
await host.RunAsync(args);
