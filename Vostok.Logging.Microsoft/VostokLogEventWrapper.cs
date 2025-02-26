using System.Collections;
using System.Collections.Generic;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Microsoft.Helpers;

namespace Vostok.Logging.Microsoft
{
    internal sealed class VostokLogEventWrapper(LogEvent logEvent) : IReadOnlyCollection<KeyValuePair<string, object>>
    {
        public LogEvent LogEvent { get; } = logEvent;

        public int Count => 1 + LogEvent.Properties?.Count ?? 0;

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            yield return new KeyValuePair<string, object>(MicrosoftLogProperties.OriginalFormatKey, LogEvent.MessageTemplate);

            if (LogEvent.Properties == null)
                yield break;

            foreach (var property in LogEvent.Properties)
                yield return property;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}