using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Abstractions.Wrappers;
using LogLevel = Vostok.Logging.Abstractions.LogLevel;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

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

            actualSourceContext.Should().Be(context);
        }

        [Test]
        public void Log_SimpleMessage_LogsValidEvent()
        {
            loggerProvider.CreateLogger(null).LogInformation("message");

            var expectedLogEvent = new LogEvent(LogLevel.Info, DateTimeOffset.Now, "message");

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

            var expectedLogEvent = new LogEvent(LogLevel.Debug, DateTimeOffset.Now, "message {p1} {p2}")
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

            var expectedLogEvent = new LogEvent(LogLevel.Fatal, DateTimeOffset.Now, "message {0} {1}")
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

            var expectedLogEvent = new LogEvent(LogLevel.Info, DateTimeOffset.Now, "message", new Exception("exception"));

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

            var expectedLogEvent = new LogEvent(LogLevel.Info, DateTimeOffset.UtcNow, "message {p1} {p2}")
                .WithProperty("Scope", "scope sv1 sv2")
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
        public void Log_InNestedScope_LogsWithNestedScope()
        {
            var logger = loggerProvider.CreateLogger(null);
            using (logger.BeginScope("s1"))
            using (logger.BeginScope("s2"))
            {
                logger.LogInformation("message {p1} {p2}", "v1", "v2");
            }

            var expectedLogEvent = new LogEvent(LogLevel.Info, DateTimeOffset.UtcNow, "message {p1} {p2}")
                .WithProperty("Scope", new[] {"s1", "s2"})
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
            loggerProvider.CreateLogger(null).LogCritical(new EventId(1, "eventId"), "message");

            var expectedLogEvent = new LogEvent(LogLevel.Fatal, DateTimeOffset.Now, "message")
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
            loggerProvider.CreateLogger(null).LogCritical(new EventId(1), "message");

            var expectedLogEvent = new LogEvent(LogLevel.Fatal, DateTimeOffset.Now, "message")
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
            loggerProvider.CreateLogger(null).LogCritical(new EventId(0, "eventId"), "message");

            var expectedLogEvent = new LogEvent(LogLevel.Fatal, DateTimeOffset.Now, "message")
                .WithProperty("EventId.Name", "eventId");

            log.Events.Single()
                .Should()
                .BeEquivalentTo(
                    expectedLogEvent,
                    o => o.Excluding(x => x.Timestamp));
        }
        
        private class MemoryLog : ILog
        {
            public List<LogEvent> Events { get; } = new List<LogEvent>();

            public void Log(LogEvent @event)
            {
                Events.Add(@event);
            }

            public bool IsEnabledFor(LogLevel level) => true;

            public ILog ForContext(string context)
            {
                return new SourceContextWrapper(this, context);
            }
        }
    }
}