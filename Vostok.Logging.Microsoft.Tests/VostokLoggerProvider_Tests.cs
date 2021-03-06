using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Vostok.Commons.Helpers.Disposable;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Abstractions.Values;
using Vostok.Logging.Microsoft.Tests.Helpers;
using LogLevel = Vostok.Logging.Abstractions.LogLevel;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;
using VostokLogEvent = Vostok.Logging.Abstractions.LogEvent;

namespace Vostok.Logging.Microsoft.Tests
{
    [TestFixture]
    public class VostokLoggerProvider_Tests
    {
        private MemoryLog log;
        private VostokLoggerProvider loggerProvider;

        [SetUp]
        public void TestSetup()
        {
            log = new MemoryLog();
            loggerProvider = new VostokLoggerProvider(log);
        }

        [TestCase(LogLevel.Warn, MsLogLevel.Error, true)]
        [TestCase(LogLevel.Warn, MsLogLevel.Warning, true)]
        [TestCase(LogLevel.Warn, MsLogLevel.Information, false)]
        public void IsEnabledFor_ReturnsValidResult(LogLevel minLevel, MsLogLevel level, bool expectedEnabled)
        {
            loggerProvider = new VostokLoggerProvider(log.WithMinimumLevel(minLevel));

            var logger = loggerProvider.CreateLogger(null);

            logger.IsEnabled(level).Should().Be(expectedEnabled);
        }

        [TestCase(null)]
        [TestCase("context")]
        public void CreateLoggerForCategory_ReturnsLoggerForValidContext(string context)
        {
            loggerProvider.CreateLogger(context).LogInformation("message");

            object actualSourceContext = null;

            log.Events.Single().Properties?.TryGetValue(WellKnownProperties.SourceContext, out actualSourceContext);

            if (context == null)
            {
                actualSourceContext.Should().BeNull();
            }
            else
            {
                actualSourceContext.Should().BeOfType<SourceContextValue>().Which.Should().Equal(context);
            }
        }

        [Test]
        public void Log_SimpleMessage_LogsValidEvent()
        {
            loggerProvider.CreateLogger(null).LogInformation("message");

            var expectedLogEvent = new VostokLogEvent(LogLevel.Info, DateTimeOffset.Now, "message");

            log.Events.Single()
                .Should()
                .BeEquivalentTo(
                    expectedLogEvent,
                    o => o.Excluding(x => x.Timestamp));
        }

        [Test]
        public void Log_MessageWithNamedPlaceholders_LogsValidEvent()
        {
            loggerProvider.CreateLogger(null).LogDebug("message {p1} {p2}", "v1", "v2");

            var expectedLogEvent = new VostokLogEvent(LogLevel.Debug, DateTimeOffset.Now, "message {p1} {p2}")
                .WithProperty("p1", "v1")
                .WithProperty("p2", "v2");

            log.Events.Single()
                .Should()
                .BeEquivalentTo(
                    expectedLogEvent,
                    o => o.Excluding(x => x.Timestamp));
        }

        [Test]
        public void Log_MessageWithPositionPlaceholders_LogsValidEvent()
        {
            loggerProvider.CreateLogger(null).LogCritical("message {0} {1}", "v1", "v2");

            var expectedLogEvent = new VostokLogEvent(LogLevel.Fatal, DateTimeOffset.Now, "message {0} {1}")
                .WithProperty("0", "v1")
                .WithProperty("1", "v2");

            log.Events.Single()
                .Should()
                .BeEquivalentTo(
                    expectedLogEvent,
                    o => o.Excluding(x => x.Timestamp));
        }

        [Test]
        public void Log_WithException_LogsValidEvent()
        {
            loggerProvider.CreateLogger(null).LogInformation(new Exception("exception"), "message");

            var expectedLogEvent = new VostokLogEvent(LogLevel.Info, DateTimeOffset.Now, "message", new Exception("exception"));

            log.Events.Single()
                .Should()
                .BeEquivalentTo(
                    expectedLogEvent,
                    o => o.Excluding(x => x.Timestamp));
        }

        [Test]
        public void Log_InScopeWithProperties_LogsWithScopeProperties()
        {
            var logger = loggerProvider.CreateLogger(null);
            using (logger.BeginScope("scope {sp1} {sp2}", "sv1", "sv2"))
            {
                logger.LogInformation("message {p1} {p2}", "v1", "v2");
            }

            var expectedLogEvent = new VostokLogEvent(LogLevel.Info, DateTimeOffset.UtcNow, "message {p1} {p2}")
                .WithProperty("operationContext", new OperationContextValue("scope sv1 sv2"))
                .WithProperty("sp1", "sv1")
                .WithProperty("sp2", "sv2")
                .WithProperty("p1", "v1")
                .WithProperty("p2", "v2");

            log.Events.Single()
                .Should()
                .BeEquivalentTo(
                    expectedLogEvent,
                    o => o.Excluding(x => x.Timestamp));
        }

        [Test]
        public void Log_AfterScopeWithProperties_LogsWithoutScopeProperties()
        {
            var logger = loggerProvider.CreateLogger(null);
            using (logger.BeginScope("scope {sp1} {sp2}", "sv1", "sv2"))
            {
                logger.LogInformation("message {p1} {p2}", "v1", "v2");
            }

            logger.LogInformation("message {p1} {p2}", "v1", "v2");

            var expectedLogEvent = new VostokLogEvent(LogLevel.Info, DateTimeOffset.UtcNow, "message {p1} {p2}")
                .WithProperty("p1", "v1")
                .WithProperty("p2", "v2");

            log.Events.Last()
                .Should()
                .BeEquivalentTo(
                    expectedLogEvent,
                    o => o.Excluding(x => x.Timestamp));
        }

        [Test]
        public void Log_InScopeWithNamedProperties_LogsWithAllScopeProperties()
        {
            var logger = loggerProvider.CreateLogger(null);
            using (logger.BeginScope(new Dictionary<string, object> {["key"] = "value"}))
            using (logger.BeginScope(new Dictionary<string, object> {["key2"] = "value2"}))
                logger.LogInformation("message");

            var expectedLogEvent = new VostokLogEvent(LogLevel.Info, DateTimeOffset.UtcNow, "message")
                .WithProperty(
                    "operationContext",
                    new OperationContextValue(
                        new[]
                        {
                            "System.Collections.Generic.Dictionary`2[System.String,System.Object]",
                            "System.Collections.Generic.Dictionary`2[System.String,System.Object]"
                        }))
                .WithProperty("key", "value")
                .WithProperty("key2", "value2");

            log.Events.Single()
                .Should()
                .BeEquivalentTo(
                    expectedLogEvent,
                    o => o.Excluding(x => x.Timestamp));
        }

        [Test]
        public void Log_InIgnoredScope_LogsWithoutScope()
        {
            loggerProvider = new VostokLoggerProvider(
                log,
                new VostokLoggerProviderSettings
                {
                    IgnoredScopes = new HashSet<string>
                    {
                        typeof(HashSet<int>).FullName
                    }
                });

            var logger = loggerProvider.CreateLogger(null);
            using (var scope1 = logger.BeginScope(new HashSet<int>()))
            using (var scope2 = logger.BeginScope(new HashSet<string>()))
            {
                scope1.Should().BeOfType<EmptyDisposable>();
                scope2.Should().NotBeOfType<EmptyDisposable>();

                logger.LogInformation("message");
            }

            var expectedLogEvent = new VostokLogEvent(LogLevel.Info, DateTimeOffset.UtcNow, "message")
                .WithProperty("operationContext", new OperationContextValue(new[] {"System.Collections.Generic.HashSet`1[System.String]"}));

            log.Events.Single()
                .Should()
                .BeEquivalentTo(
                    expectedLogEvent,
                    o => o.Excluding(x => x.Timestamp));
        }

        [Test]
        public void Log_InIgnoredScopePrefix_LogsWithoutScope()
        {
            loggerProvider = new VostokLoggerProvider(
                log,
                new VostokLoggerProviderSettings
                {
                    IgnoredScopePrefixes = new HashSet<string> { "system" }
                });

            var logger = loggerProvider.CreateLogger(null);
            using (var scope1 = logger.BeginScope(new HashSet<int>()))
            using (var scope2 = logger.BeginScope(new HashSet<string>()))
            {
                scope1.Should().BeOfType<EmptyDisposable>();
                scope2.Should().BeOfType<EmptyDisposable>();

                logger.LogInformation("message");
            }

            var expectedLogEvent = new VostokLogEvent(LogLevel.Info, DateTimeOffset.UtcNow, "message");

            log.Events.Single()
                .Should()
                .BeEquivalentTo(
                    expectedLogEvent,
                    o => o.Excluding(x => x.Timestamp));
        }

        [Test]
        public void Log_InNestedScope_LogsWithNestedScope()
        {
            var logger = loggerProvider.CreateLogger(null);
            using (logger.BeginScope("s1"))
            using (logger.BeginScope("s2"))
            {
                logger.LogInformation("message {p1} {p2}", "v1", "v2");
            }

            var expectedLogEvent = new VostokLogEvent(LogLevel.Info, DateTimeOffset.UtcNow, "message {p1} {p2}")
                .WithProperty("operationContext", new OperationContextValue(new[] {"s1", "s2"}))
                .WithProperty("p1", "v1")
                .WithProperty("p2", "v2");

            log.Events.Single()
                .Should()
                .BeEquivalentTo(
                    expectedLogEvent,
                    o => o.Excluding(x => x.Timestamp));
        }

        [Test]
        public void Should_create_a_timestamp_with_local_time_zone()
        {
            loggerProvider.CreateLogger(null).LogInformation("message");

            log.Events.Should().ContainSingle().Which.Timestamp.Offset.Should().Be(DateTimeOffset.Now.Offset);
        }

        [Test]
        public void Log_WithEventIdWithIdAndName_LogsValidEvent()
        {
            loggerProvider = new VostokLoggerProvider(log, new VostokLoggerProviderSettings {AddEventIdProperties = true});

            loggerProvider.CreateLogger(null).LogCritical(new EventId(1, "eventId"), "message");

            var expectedLogEvent = new VostokLogEvent(LogLevel.Fatal, DateTimeOffset.Now, "message")
                .WithProperty("EventId.Id", 1)
                .WithProperty("EventId.Name", "eventId");

            log.Events.Single()
                .Should()
                .BeEquivalentTo(
                    expectedLogEvent,
                    o => o.Excluding(x => x.Timestamp));
        }

        [Test]
        public void Log_WithEventIdWithIdOnly_LogsValidEvent()
        {
            loggerProvider = new VostokLoggerProvider(log, new VostokLoggerProviderSettings { AddEventIdProperties = true });

            loggerProvider.CreateLogger(null).LogCritical(new EventId(1), "message");

            var expectedLogEvent = new VostokLogEvent(LogLevel.Fatal, DateTimeOffset.Now, "message")
                .WithProperty("EventId.Id", 1);

            log.Events.Single()
                .Should()
                .BeEquivalentTo(
                    expectedLogEvent,
                    o => o.Excluding(x => x.Timestamp));
        }

        [Test]
        public void Log_WithEventIdWithNAMEOnly_LogsValidEvent()
        {
            loggerProvider = new VostokLoggerProvider(log, new VostokLoggerProviderSettings { AddEventIdProperties = true });

            loggerProvider.CreateLogger(null).LogCritical(new EventId(0, "eventId"), "message");

            var expectedLogEvent = new VostokLogEvent(LogLevel.Fatal, DateTimeOffset.Now, "message")
                .WithProperty("EventId.Name", "eventId");

            log.Events.Single()
                .Should()
                .BeEquivalentTo(
                    expectedLogEvent,
                    o => o.Excluding(x => x.Timestamp));
        }

        [Test]
        public void Log_InScopeInDifferentAsyncLocalContexts_LogsInValidScopes()
        {
            var logger = loggerProvider.CreateLogger(null);

            var start = new ManualResetEventSlim(false);
            var wait = new ManualResetEventSlim(false);

            var task = Task.Run(
                () =>
                {
                    using (logger.BeginScope("inner-scope"))
                    {
                        start.Set();
                        wait.Wait();
                    }
                });

            start.Wait();
            using (logger.BeginScope("outer-scope"))
                logger.LogInformation("message");
            wait.Set();
            task.Wait();

            var expectedLogEvent = new VostokLogEvent(LogLevel.Info, DateTimeOffset.UtcNow, "message")
                .WithProperty("operationContext", new OperationContextValue("outer-scope"));

            log.Events.Single()
                .Should()
                .BeEquivalentTo(
                    expectedLogEvent,
                    o => o.Excluding(x => x.Timestamp));
        }
    }
}