using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Hosting;

namespace RtFlow.Pipelines.Core;

public class PipelineFactory : IPipelineFactory
{
    private readonly CancellationToken _stopping;
    public PipelineFactory(IHostApplicationLifetime lifetime)
        => _stopping = lifetime.ApplicationStopping;

    public IFluentPipelineBuilder<T, T> Create<T>(Action<ExecutionDataflowBlockOptions> cfg = null)
        // specify <T> here
        => FluentPipeline.Create<T>(cfg, _stopping);
        
    public IFluentPipelineBuilder<TIn, TOut> BeginWith<TIn, TOut>(
        IPropagatorBlock<TIn, TOut> head,
        Action<ExecutionDataflowBlockOptions> cfg = null)
        // and here <TIn, TOut>
        => FluentPipeline.BeginWith(head, _stopping);
}
