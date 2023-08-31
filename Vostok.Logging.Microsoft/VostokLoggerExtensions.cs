using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Vostok.Logging.Abstractions;

namespace Vostok.Logging.Microsoft
{
    /// <summary>
    /// Extension methods allowing to add Vostok logging implementation to Microsoft.Extensions.Logging subsystem.
    /// </summary>
    [PublicAPI]
    public static class VostokLoggerExtensions
    {
        /// <summary>
        /// <para>Add a new <see cref="VostokLoggerProvider"/> for given root <paramref name="log"/> to the <paramref name="factory"/></para>
        /// </summary>
        [NotNull]
        public static ILoggerFactory AddVostok([NotNull] this ILoggerFactory factory, [NotNull] ILog log)
        {
            factory.AddProvider(new VostokLoggerProvider(log));
            return factory;
        }

        /// <summary>
        /// <para>Add a new <see cref="VostokLoggerProvider"/> for given root <paramref name="log"/> to the <paramref name="builder"/></para>
        /// </summary>
        [NotNull]
        public static ILoggingBuilder AddVostok([NotNull] this ILoggingBuilder builder, [NotNull] ILog log)
        {
            builder.AddProvider(new VostokLoggerProvider(log));
            return builder;
        }

        /// <summary>
        /// <para>Create a new <see cref="MicrosoftLog"/> with given <paramref name="categoryName"/> from the <paramref name="factory"/></para>
        /// </summary>
        [NotNull]
        public static MicrosoftLog CreateVostokMicrosoftLog([NotNull] this ILoggerFactory factory, string categoryName)
        {
            var logger = factory.CreateLogger(categoryName);
            return new MicrosoftLog(logger);
        }
    }
}