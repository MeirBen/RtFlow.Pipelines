using System.Threading.Tasks.Dataflow;

namespace RtFlow.Pipelines.Core;

public interface IPipelineFactory
{
    IFluentPipelineBuilder<T, T> Create<T>(Action<ExecutionDataflowBlockOptions> configure = null);
    IFluentPipelineBuilder<TIn, TOut> BeginWith<TIn, TOut>(
      IPropagatorBlock<TIn, TOut> head,
      Action<ExecutionDataflowBlockOptions> configure = null);
}
