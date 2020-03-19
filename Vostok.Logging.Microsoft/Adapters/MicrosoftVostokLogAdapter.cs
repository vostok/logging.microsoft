using System;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Internal;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Abstractions.Wrappers;
using Vostok.Logging.Formatting;
using VostokLogLevel = Vostok.Logging.Abstractions.LogLevel;
using MicrosoftLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Vostok.Logging.Microsoft.Adapters
{
    [PublicAPI]
    public sealed class MicrosoftVostokLogAdapter : ILog
    {
        private const int NullEventId = 0;
        private readonly Func<FormattedLogValues, Exception, string> messageFormatter = MessageFormatter;

        private readonly ILogger logger;
        private readonly OutputTemplate template = OutputTemplate.Parse(
            $"{{{WellKnownProperties.TraceContext}:w}}" +
            $"{{{WellKnownProperties.OperationContext}:w}}" +
            $"{{{WellKnownProperties.SourceContext}:w}}" +
            $"{{{WellKnownTokens.Message}}}");

        public MicrosoftVostokLogAdapter(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Log(LogEvent @event)
        {
            if (@event is null)
                return;

            if (!IsEnabledFor(@event.Level))
                return;

            var logLevel = ConvertLogLevel(@event.Level);
            var message = LogEventFormatter.Format(@event, template);
            var state = new FormattedLogValues(message, Array.Empty<object>());

            logger.Log(logLevel, NullEventId, state, @event.Exception, messageFormatter);
        }

        private static string MessageFormatter(FormattedLogValues state, Exception error)
        {
            return state.ToString();
        }

        public bool IsEnabledFor(VostokLogLevel level)
        {
            var logLevel = ConvertLogLevel(level);
            return logger.IsEnabled(logLevel);
        }

        public ILog ForContext(string context)
        {
            return new SourceContextWrapper(this, context);
        }

        private static MicrosoftLogLevel ConvertLogLevel(VostokLogLevel logLevel)
        {
            switch (logLevel)
            {
                case VostokLogLevel.Debug:
                    return MicrosoftLogLevel.Debug;
                case VostokLogLevel.Info:
                    return MicrosoftLogLevel.Information;
                case VostokLogLevel.Warn:
                    return MicrosoftLogLevel.Warning;
                case VostokLogLevel.Error:
                    return MicrosoftLogLevel.Error;
                case VostokLogLevel.Fatal:
                    return MicrosoftLogLevel.Critical;
                default:
                    return MicrosoftLogLevel.None;
            }
        }
    }
}