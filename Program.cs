using DStreamDotnetTest;
using Katasec.DStream.Plugin;

var host = new DStreamPluginHost<CounterPlugin>();

// Run the plugin host
await host.RunAsync(args);
