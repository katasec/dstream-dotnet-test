// IDStreamPlugin.cs
// Core interface for dstream plugins

using DStream.Plugin;

namespace DStreamDotnetTest
{
    /// <summary>
    /// Core interface for dstream plugins
    /// Plugin developers only need to implement this interface
    /// </summary>
    public interface IDStreamPlugin
    {
        /// <summary>
        /// The main execution method for the plugin
        /// This will be called when the plugin is started by dstream
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for graceful shutdown</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task ExecuteAsync(CancellationToken cancellationToken);
        
        /// <summary>
        /// Gets the schema fields for the plugin
        /// </summary>
        /// <returns>A dictionary of field schemas</returns>
        IEnumerable<FieldSchema> GetSchemaFields();
        
        /// <summary>
        /// Gets the name of the plugin module for logging
        /// </summary>
        string ModuleName { get; }
    }
}
