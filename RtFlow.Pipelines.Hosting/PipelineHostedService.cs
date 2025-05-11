using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Hosting;
using RtFlow.Pipelines.Core;

namespace RtFlow.Pipelines.Hosting
{
    public class PipelineHostedService : IHostedService
    {
        private readonly IEnumerable<IPipelineDefinition> _definitions;
        private readonly List<IDisposable> _links = new();

        public PipelineHostedService(IEnumerable<IPipelineDefinition> definitions)
            => _definitions = definitions;

        public Task StartAsync(CancellationToken ct)
        {
            foreach (var def in _definitions)
            {
                var block = def.Create(ct);
                // Optionally expose block to DI, or wire up further links here.
                // Collect for shutdown:
                _links.Add(new PipelineHandle(def.Name, block));
            }
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken ct)
        {
            // Signal completion to all blocks
            foreach (var h in _links.OfType<PipelineHandle>())
                h.Block.Complete();

            // Await their Completion tasks
            await Task.WhenAll(_links
                .OfType<PipelineHandle>()
                .Select(h => h.Block.Completion)
            );
        }

        // simple holder
        private record PipelineHandle(string Name, IDataflowBlock Block) : IDisposable
        {
            public void Dispose() { /*nothing*/ }
        }
    }
}
