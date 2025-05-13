using Microsoft.Extensions.Hosting;

namespace RtFlow.Pipelines.Hosting
{
    public class PipelineHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
