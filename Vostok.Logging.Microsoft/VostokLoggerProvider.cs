using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Vostok.Commons.Helpers.Disposable;
using Vostok.Commons.Time;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Context;
using Vostok.Logging.Microsoft.Helpers;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Vostok.Logging.Microsoft
{
    /// <inheritdoc />
    /// <summary>
    /// <para>Logger provider for writing log events into Vostok <see cref="ILog"/>.</para>
    /// </summary>
    [PublicAPI]
    public class VostokLoggerProvider : ILoggerProvider
    {
        private static readonly EmptyDisposable emptyDisposable = new EmptyDisposable();

        private readonly ILog log;
        private readonly VostokLoggerProviderSettings settings;

        /// <summary>
        /// <para>Create a new <see cref="VostokLoggerProvider"/> for given root <paramref name="log"/>.</para>
        /// </summary>
        /// <param name="log"><see cref="ILog"/> to write log events to.</param>
        // ReSharper disable once RedundantOverload.Global
        public VostokLoggerProvider([NotNull] ILog log)
            : this(log, null)
        {
        }

        /// <summary>
        /// <para>Create a new <see cref="VostokLoggerProvider"/> for given root <paramref name="log"/>.</para>
        /// </summary>
        /// <param name="log"><see cref="ILog"/> to write log events to.</param>
        public VostokLoggerProvider([NotNull] ILog log, [CanBeNull] VostokLoggerProviderSettings settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? new VostokLoggerProviderSettings();
        }

        /// <inheritdoc />
        /// <summary>
        /// <para>Create a new <see cref="ILogger"/> for given <paramref name="categoryName"/></para> 
        /// <para>
        /// If <paramref name="categoryName"/> is null or empty then returned <see cref="ILogger"/> will write events directly into <see cref="log"/>,
        /// otherwise it will write events into <see cref="log"/>.<see cref="ILog.ForContext"/> with <paramref name="categoryName"/> used as context
        /// </para> 
        /// </summary>
        [NotNull]
        public ILogger CreateLogger([CanBeNull] string categoryName)
        {
            return new Logger(
                string.IsNullOrEmpty(categoryName) ? log : log.ForContext(categoryName), 
                settings.IgnoredScopes,
                settings.IgnoredScopePrefixes,
                settings.AddEventIdProperties);
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }

        internal class Logger : ILogger
        {
            private readonly ILog log;
            private readonly IReadOnlyCollection<string> ignoredScopes;
            private readonly IReadOnlyCollection<string> ignoredScopePrefixes;
            private readonly bool addEventIdProperties;
            private readonly AsyncLocal<UseScope> scope = new AsyncLocal<UseScope>();

            public Logger(
                ILog log, 
                IReadOnlyCollection<string> ignoredScopes,
                IReadOnlyCollection<string> ignoredScopePrefixes,
                bool addEventIdProperties)
            {
                this.log = log;
                this.ignoredScopes = ignoredScopes;
                this.ignoredScopePrefixes = ignoredScopePrefixes;
                this.addEventIdProperties = addEventIdProperties;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception exception,
                Func<TState, Exception, string> formatter)
            {
                if (logLevel == LogLevel.None)
                    return;

                var localLog = scope.Value?.Log ?? log;

                if (state is VostokLogEventWrapper logEventWrapper)
                {
                    localLog.Log(logEventWrapper.LogEvent);
                    return;
                }

                var translatedLevel = TranslateLogLevel(logLevel);
                if (!log.IsEnabledFor(translatedLevel))
                    return;

                var messageTemplate = ExtractMessageTemplate(state, exception, formatter);
                var logEvent = EnrichWithProperties(new LogEvent(translatedLevel, PreciseDateTime.Now, messageTemplate, exception), eventId, state);
                
                localLog.Log(logEvent);
            }

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && log.IsEnabledFor(TranslateLogLevel(logLevel));

            public IDisposable BeginScope<TState>(TState state)
            {
                var scopeName = typeof(TState).FullName;

                if (ignoredScopes?.Contains(scopeName) == true)
                    return emptyDisposable;

                if (ignoredScopePrefixes != null)
                    foreach (var prefix in ignoredScopePrefixes)
                    {
                        if (scopeName?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true)
                            return emptyDisposable;
                    }

                var scopeValue = state == null ? scopeName : Convert.ToString(state);
                var scopeLog = scope.Value?.Log ?? log.WithOperationContext();

                if (state is IEnumerable<KeyValuePair<string, object>> props)
                {
                    foreach (var kvp in props)
                    {
                        if (kvp.Key != MicrosoftLogProperties.OriginalFormatKey)
                        {
                            scopeLog = scopeLog.WithProperty(kvp.Key, kvp.Value);
                        }
                    }
                }

                return new UseScope(scopeLog, scopeValue, scope);
            }

            private LogEvent EnrichWithProperties<TState>(LogEvent logEvent, EventId eventId, TState state)
            {
                if (state is IEnumerable<KeyValuePair<string, object>> props)
                {
                    foreach (var kvp in props)
                    {
                        if (kvp.Key == MicrosoftLogProperties.OriginalFormatKey)
                            continue;
                        logEvent = logEvent.WithPropertyIfAbsent(kvp.Key, kvp.Value);
                    }
                }

                if (addEventIdProperties)
                {
                    if (eventId.Id != 0)
                        logEvent = logEvent.WithPropertyIfAbsent("EventId.Id", eventId.Id);

                    if (!string.IsNullOrEmpty(eventId.Name))
                        logEvent = logEvent.WithPropertyIfAbsent("EventId.Name", eventId.Name);
                }

                return logEvent;
            }

            private static string ExtractMessageTemplate<TState>(TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (state is IEnumerable<KeyValuePair<string, object>> props)
                {
                    foreach (var kvp in props)
                    {
                        if (kvp.Key == MicrosoftLogProperties.OriginalFormatKey)
                            return Convert.ToString(kvp.Value);
                    }
                }

                if (formatter != null)
                    return formatter(state, exception);

                return ReferenceEquals(state, null) ? typeof(TState).FullName : Convert.ToString(state);
            }

            private static Abstractions.LogLevel TranslateLogLevel(LogLevel logLevel)
            {
                switch (logLevel)
                {
                    case LogLevel.Trace:
                    case LogLevel.Debug:
                        return Abstractions.LogLevel.Debug;
                    case LogLevel.Information:
                        return Abstractions.LogLevel.Info;
                    case LogLevel.Warning:
                        return Abstractions.LogLevel.Warn;
                    case LogLevel.Error:
                        return Abstractions.LogLevel.Error;
                    case LogLevel.Critical:
                        return Abstractions.LogLevel.Fatal;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
                }
            }

            private class UseScope : IDisposable
            {
                private readonly OperationContextToken operationContextToken;
                private readonly AsyncLocal<UseScope> scope;
                private readonly UseScope previousScopeValue;

                public UseScope(ILog log, string scopeValue, AsyncLocal<UseScope> scope)
                {
                    Log = log;
                    this.scope = scope;
                    operationContextToken = new OperationContextToken(scopeValue);
                    previousScopeValue = scope.Value;
                    scope.Value = this;
                }

                public ILog Log { get; }

                public void Dispose()
                {
                    // ReSharper disable once PossiblyImpureMethodCallOnReadonlyVariable
                    operationContextToken.Dispose();
                    scope.Value = previousScopeValue;
                }
            }
        }
    }
}