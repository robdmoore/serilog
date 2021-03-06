﻿// Copyright 2013 Nicholas Blumhardt
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.IO;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Sinks.IOTextWriter;

namespace Serilog.Configuration
{
    /// <summary>
    /// Controls sink configuration.
    /// </summary>
    public class LoggerSinkConfiguration
    {
        readonly LoggerConfiguration _loggerConfiguration;
        readonly Action<ILogEventSink> _addSink;

        const string DefaultOutputTemplate = "{Timestamp} [{Level}] {Message:l}{NewLine:l}{Exception:l}";

        internal LoggerSinkConfiguration(LoggerConfiguration loggerConfiguration, Action<ILogEventSink> addSink)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException("loggerConfiguration");
            if (addSink == null) throw new ArgumentNullException("addSink");
            _loggerConfiguration = loggerConfiguration;
            _addSink = addSink;
        }

        /// <summary>
        /// Write log events to the specified <see cref="ILogEventSink"/>.
        /// </summary>
        /// <param name="logEventSink">The sink.</param>
        /// <param name="restrictedToMinimumLevel">The minimum level for
        /// events passed through the sink.</param>
        /// <returns>Configuration object allowing method chaining.</returns>
        public LoggerConfiguration Sink(
            ILogEventSink logEventSink,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum)
        {
            var sink = logEventSink;
            if (restrictedToMinimumLevel > LevelAlias.Minimum)
                sink = new RestrictedSink(sink, restrictedToMinimumLevel);

            _addSink(sink);
            return _loggerConfiguration;
        }

        /// <summary>
        /// Write log events to the provided <see cref="TextWriter"/>.
        /// </summary>
        /// <param name="textWriter">The text writer to write log events to.</param>
        /// <param name="outputTemplate">Message template describing the output format.</param>
        /// <param name="restrictedToMinimumLevel">The minimum level for
        /// events passed through the sink.</param>
        /// <returns>Configuration object allowing method chaining.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public LoggerConfiguration TextWriter(
            TextWriter textWriter,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string outputTemplate = DefaultOutputTemplate)
        {
            if (textWriter == null) throw new ArgumentNullException("textWriter");
            if (outputTemplate == null) throw new ArgumentNullException("outputTemplate");

            var formatter = new MessageTemplateTextFormatter(outputTemplate);
            var sink = new TextWriterSink(textWriter, formatter);
            return Sink(sink);
        }
    }
}
