using DStreamDotnetTest;

var host = new DStreamPluginHost<CounterPlugin>();

// Run the plugin host
await host.RunAsync(args);
