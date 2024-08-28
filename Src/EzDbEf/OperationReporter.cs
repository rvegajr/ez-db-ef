#pragma warning disable EF1001 // Internal EF Core API usage.
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.Extensions.Logging;

namespace EzDbEf
{
    public class OperationReporter : IOperationReporter
    {
        private readonly ILogger _logger;

        public OperationReporter(ILogger logger)
        {
            _logger = logger;
        }

        public void WriteError(string message) => _logger.LogError(message);
        public void WriteInformation(string message) => _logger.LogInformation(message);
        public void WriteVerbose(string message) => _logger.LogDebug(message);
        public void WriteWarning(string message) => _logger.LogWarning(message);
    }
}
#pragma warning restore EF1001