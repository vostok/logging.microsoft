using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Vostok.Logging.Microsoft
{
    [PublicAPI]
    public class VostokLoggerProviderSettings
    {
        /// <summary>
        /// Full type names that should be ignored in <see cref="ILogger.BeginScope{TState}"></see>./>
        /// </summary>
        [CanBeNull]
        public IReadOnlyCollection<string> IgnoredScopes { get; set; }

        /// <summary>
        /// Full type name prefixes that should be ignored in <see cref="ILogger.BeginScope{TState}"></see>./>
        /// </summary>
        [CanBeNull]
        public IReadOnlyCollection<string> IgnoredScopePrefixes { get; set; }

        public bool AddEventIdProperties { get; set; }
    }
}