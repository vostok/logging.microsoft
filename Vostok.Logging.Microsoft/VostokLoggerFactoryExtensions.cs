using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Vostok.Logging.Abstractions;

namespace Vostok.Logging.Microsoft
{
    /// <summary>
    /// Extension methods for use vostok logging in Microsoft.Extensions.Logging  
    /// </summary>
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
    }
}