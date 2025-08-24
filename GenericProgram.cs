// GenericProgram.cs
// Entry point for the generic counter plugin

using DStreamDotnetTest;
using Katasec.DStream.Plugin;
using HCLog.Net;
namespace DStreamDotnetTest
{
    public class GenericProgram
    {
        // This class is not currently used, but kept for reference
        // The main entry point is in Program.cs
        public static async Task Main(string[] args)
        {
            // Create and run the plugin host with strongly-typed configuration
            var host = new DStreamPluginHost<GenericCounterPlugin, GenericCounterConfig>();
            await host.RunAsync(args);
        }
    }
}
