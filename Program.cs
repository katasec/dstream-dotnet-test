// This is a .NET gRPC plugin for dstream that implements the HashiCorp go-plugin handshake protocol
// It outputs an infinite counter every 5 seconds and ensures proper handshake with dstream
// This version uses a simplified plugin framework for easier plugin development
using System;
using System.Threading.Tasks;

namespace DStreamDotnetTest
{
    /// <summary>
    /// Main program class
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Entry point for the application
        /// </summary>
        static async Task Main(string[] args)
        {
            // Create the plugin host for our CounterPlugin
            var host = new DStreamPluginHost<CounterPlugin>();
            
            // Run the plugin host
            await host.RunAsync(args);
        }
    }
}
