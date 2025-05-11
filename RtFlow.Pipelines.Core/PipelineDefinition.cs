using System.Threading.Tasks.Dataflow;

namespace RtFlow.Pipelines.Core
{
    public class PipelineDefinition<TIn, TOut> : IPipelineDefinition
    {
        public string Name { get; }
        private readonly Func<CancellationToken, IPropagatorBlock<TIn, TOut>> _factory;
        public PipelineDefinition(
            string name,
            Func<CancellationToken, IPropagatorBlock<TIn, TOut>> factory)
        {
            Name = name;
            _factory = factory;
        }

        // add this public helper:
        public IPropagatorBlock<TIn, TOut> Create(CancellationToken ct)
            => _factory(ct);

        // satisfy the interface explicitly by forwarding:
        IDataflowBlock IPipelineDefinition.Create(CancellationToken ct)
            => Create(ct);
    }
}
