// HCLogger.cs
// HashiCorp compatible logger for .NET that implements the hclog format
// This logger can be used with HashiCorp's go-plugin system to send logs from .NET plugins to a Go host process

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DStreamDotnetTest
{
    /// <summary>
    /// HashiCorp compatible logger implementation that exactly matches the format expected by go-plugin
    /// </summary>
    public class HCLogger
    {
        private readonly string _name;
        private readonly TextWriter _writer;
        
        /// <summary>
        /// Creates a new HashiCorp compatible logger
        /// </summary>
        /// <param name="name">The name/module of the logger</param>
        /// <param name="writer">Optional TextWriter to write logs to (defaults to Console.Error)</param>
        public HCLogger(string name, TextWriter writer = null)
        {
            _name = name;
            _writer = writer ?? Console.Error; // HashiCorp go-plugin uses stderr for logging
        }
        
        /// <summary>
        /// Logs a message with the specified level
        /// </summary>
        /// <param name="level">Log level (debug, info, warn, error)</param>
        /// <param name="message">Message format string</param>
        /// <param name="args">Optional format arguments</param>
        public void Log(string level, string message, params object[] args)
        {
            try
            {
                // Format the message with args if any
                string formattedMessage = args.Length > 0 ? string.Format(message, args) : message;
                
                // Create the log entry in HashiCorp hclog format
                // The format must exactly match what HashiCorp's go-plugin expects
                var logEntry = new Dictionary<string, object>
                {
                    ["@level"] = level,
                    ["@message"] = formattedMessage,
                    ["@module"] = _name
                };
                
                // Serialize to JSON and write to stderr
                string json = JsonSerializer.Serialize(logEntry);
                _writer.WriteLine(json);
                _writer.Flush();
            }
            catch (Exception ex)
            {
                // If logging fails, write a simple error message to stderr
                // This shouldn't happen in normal operation
                try
                {
                    _writer.WriteLine($"{{\"@level\":\"error\",\"@message\":\"Logging error: {ex.Message}\",\"@module\":\"{_name}\"}}");
                    _writer.Flush();
                }
                catch
                {
                    // Last resort - if we can't even log the error, just ignore it
                }
            }
        }
        
        /// <summary>
        /// Logs a debug message
        /// </summary>
        /// <param name="message">Message format string</param>
        /// <param name="args">Optional format arguments</param>
        public void Debug(string message, params object[] args) => Log("debug", message, args);
        
        /// <summary>
        /// Logs an info message
        /// </summary>
        /// <param name="message">Message format string</param>
        /// <param name="args">Optional format arguments</param>
        public void Info(string message, params object[] args) => Log("info", message, args);
        
        /// <summary>
        /// Logs a warning message
        /// </summary>
        /// <param name="message">Message format string</param>
        /// <param name="args">Optional format arguments</param>
        public void Warn(string message, params object[] args) => Log("warn", message, args);
        
        /// <summary>
        /// Logs an error message
        /// </summary>
        /// <param name="message">Message format string</param>
        /// <param name="args">Optional format arguments</param>
        public void Error(string message, params object[] args) => Log("error", message, args);
    }
}
