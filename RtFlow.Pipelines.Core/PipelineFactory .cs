using System.Threading.Tasks.Dataflow;

namespace RtFlow.Pipelines.Core;

public class PipelineFactory : IPipelineFactory
{

    public IFluentPipelineBuilder<T, T> Create<T>
    (Action<ExecutionDataflowBlockOptions> cfg = null,
        CancellationToken stopping = default)
        // specify <T> here
        => FluentPipeline.Create<T>(cfg, stopping);

    public IFluentPipelineBuilder<TIn, TOut> BeginWith<TIn, TOut>(
        IPropagatorBlock<TIn, TOut> head,
        CancellationToken stopping = default)
        // and here <TIn, TOut>
        => FluentPipeline.BeginWith(head, stopping);
}
