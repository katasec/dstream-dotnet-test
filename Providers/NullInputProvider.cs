using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Katasec.DStream.Plugin.Interfaces;
using Katasec.DStream.Plugin.Models;

namespace DStreamDotnetTest.Providers
{
    /// <summary>
    /// A null input provider that doesn't provide any input
    /// </summary>
    public class NullInputProvider : IInput
    {
        /// <summary>
        /// Gets the name of this input provider
        /// </summary>
        public string Name => "null";

        /// <summary>
        /// Initializes the input provider with the given configuration
        /// </summary>
        /// <param name="config">Provider-specific configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public Task InitializeAsync(Dictionary<string, object> config, CancellationToken cancellationToken)
        {
            // No initialization needed for null provider
            return Task.CompletedTask;
        }

        /// <summary>
        /// Reads the next batch of items from the input source
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A collection of stream items</returns>
        public Task<IEnumerable<StreamItem>> ReadAsync(CancellationToken cancellationToken)
        {
            // Return an empty collection as this is a null provider
            return Task.FromResult<IEnumerable<StreamItem>>(new StreamItem[0]);
        }

        /// <summary>
        /// Determines if there is more data available to read
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if more data is available, false otherwise</returns>
        public Task<bool> HasMoreDataAsync(CancellationToken cancellationToken)
        {
            // Always return false as this provider never has data
            return Task.FromResult(false);
        }

        /// <summary>
        /// Performs cleanup operations when the input provider is no longer needed
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public Task CloseAsync(CancellationToken cancellationToken)
        {
            // No cleanup needed
            return Task.CompletedTask;
        }
    }
}
