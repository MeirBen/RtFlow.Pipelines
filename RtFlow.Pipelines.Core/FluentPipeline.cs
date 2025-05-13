using System.Threading.Tasks.Dataflow;
using RtFlow.Pipelines.Core.Utils;

namespace RtFlow.Pipelines.Core;

/// <summary>
/// Entry-point for creating a fluent pipeline.
/// </summary>
public static class FluentPipeline
{
    /// <summary>
    /// Start a pipeline that buffers and propagates the same type.
    /// </summary>
    public static IFluentPipelineBuilder<T, T> Create<T>(
        Action<ExecutionDataflowBlockOptions> configureBuffer = null)
    {
        var opts = new ExecutionDataflowBlockOptions();
        configureBuffer?.Invoke(opts);
        // BufferBlock<T> implements IPropagatorBlock<T,T>
        var buffer = new BufferBlock<T>(opts);
        var inner = PipelineBuilder.BeginWith<T, T>(buffer);
        return new FluentPipelineBuilder<T, T>(inner);
    }

    /// <summary>
    /// Start a pipeline from an existing propagator block.
    /// </summary>
    public static IFluentPipelineBuilder<TIn, TOut> BeginWith<TIn, TOut>(
        IPropagatorBlock<TIn, TOut> head)
    {
        var inner = PipelineBuilder.BeginWith(head);
        return new FluentPipelineBuilder<TIn, TOut>(inner);
    }
}