using System.Threading.Tasks.Dataflow;
using RtFlow.Pipelines.Core.Utils;

namespace RtFlow.Pipelines.Core;

/// <summary>
/// Entry-point for creating a fluent pipeline.
/// </summary>
public static class FluentPipeline
{
    /// <summary>
    /// Start a pipeline with a BufferBlock as the entry point.
    /// </summary>
    /// <typeparam name="T">The type of data that flows through the initial buffer</typeparam>    /// <param name="configureBuffer">Optional action to configure the buffer block options</param>
    /// <param name="cancellationToken">Optional cancellation token to control pipeline lifetime</param>
    /// <returns>A fluent pipeline builder that can be used to continue building the pipeline</returns>
    public static IFluentPipelineBuilder<T, T> Create<T>(
        Action<ExecutionDataflowBlockOptions> configureBuffer = null,
        CancellationToken cancellationToken = default)
    {
        var opts = new ExecutionDataflowBlockOptions
        {
            CancellationToken = cancellationToken
        };
        configureBuffer?.Invoke(opts);

        var buffer = new BufferBlock<T>(opts);
        var inner = PipelineBuilder.BeginWith(buffer);
        return new FluentPipelineBuilder<T, T>(inner, cancellationToken);    }

    /// <summary>
    /// Start a pipeline with an existing propagator block as the entry point.
    /// The generic parameters <typeparamref name="TIn"/> and <typeparamref name="TOut"/> represent 
    /// the input and output types of the provided block, allowing the pipeline to begin with 
    /// any compatible block type and preserving type information for the fluent chain.
    /// </summary>
    /// <typeparam name="TIn">The input type accepted by the propagator block</typeparam>
    /// <typeparam name="TOut">The output type produced by the propagator block</typeparam>
    /// <param name="head">The propagator block to use as the starting point of the pipeline</param>
    /// <param name="cancellationToken">Optional cancellation token to control pipeline lifetime</param>
    /// <returns>A fluent pipeline builder that can be used to continue building the pipeline</returns>
    public static IFluentPipelineBuilder<TIn, TOut> BeginWith<TIn, TOut>(
        IPropagatorBlock<TIn, TOut> head,
        CancellationToken cancellationToken = default)
    {
        var inner = PipelineBuilder.BeginWith(head);
        return new FluentPipelineBuilder<TIn, TOut>(inner, cancellationToken);
    }
}