using System.Threading.Tasks.Dataflow;

namespace RtFlow.Pipelines.Core;

/// <summary>
/// Defines a fluent interface for building dataflow pipelines.
/// </summary>
/// <typeparam name="TIn">Input type of the pipeline.</typeparam>
/// <typeparam name="TOut">Output type of the pipeline.</typeparam>
public interface IFluentPipelineBuilder<TIn, TOut>
{
    /// <summary>
    /// Adds a synchronous transformation step to the pipeline.
    /// </summary>
    /// <typeparam name="TNext">Output type of the transformation.</typeparam>
    /// <param name="selector">Function to transform each element.</param>
    /// <param name="configure">Optional action to configure block options.</param>
    public IFluentPipelineBuilder<TIn, TNext> Transform<TNext>(
        Func<TOut, TNext> selector,
        Action<ExecutionDataflowBlockOptions> configure = null);

    /// <summary>
    /// Adds an asynchronous transformation step to the pipeline.
    /// </summary>
    /// <typeparam name="TNext">Output type of the transformation.</typeparam>
    /// <param name="selector">Async function to transform each element.</param>
    /// <param name="configure">Optional action to configure block options.</param>
    public IFluentPipelineBuilder<TIn, TNext> TransformAsync<TNext>(
        Func<TOut, Task<TNext>> selector,
        Action<ExecutionDataflowBlockOptions> configure = null);

    /// <summary>
    /// Adds an asynchronous transformation step with cancellation support.
    /// </summary>
    /// <typeparam name="TNext">Output type of the transformation.</typeparam>
    /// <param name="selector">Async function with cancellation support.</param>
    /// <param name="configure">Optional action to configure block options.</param>
    public IFluentPipelineBuilder<TIn, TNext> TransformAsync<TNext>(
        Func<TOut, CancellationToken, Task<TNext>> selector,
        Action<ExecutionDataflowBlockOptions> configure = null);

    /// <summary>
    /// Adds a side effect without changing the data.
    /// </summary>
    /// <param name="sideEffect">Action to perform on each element.</param>
    IFluentPipelineBuilder<TIn, TOut> Tap(Action<TOut> sideEffect);

    /// <summary>
    /// Adds an asynchronous side effect without changing the data.
    /// </summary>
    /// <param name="sideEffect">Async action to perform on each element.</param>
    IFluentPipelineBuilder<TIn, TOut> TapAsync(Func<TOut, Task> sideEffect);

    /// <summary>
    /// Adds an asynchronous side effect with cancellation support.
    /// </summary>
    /// <param name="sideEffect">Async action with cancellation support.</param>
    IFluentPipelineBuilder<TIn, TOut> TapAsync(Func<TOut, CancellationToken, Task> sideEffect);

    /// <summary>
    /// Groups elements into batches of the specified size.
    /// </summary>
    /// <param name="batchSize">Size of each batch.</param>
    /// <param name="configure">Optional action to configure batch options.</param>
    IFluentPipelineBuilder<TIn, TOut[]> Batch(
        int batchSize,
        Action<GroupingDataflowBlockOptions> configure = null);

    /// <summary>
    /// Builds and returns the pipeline as a propagator block.
    /// </summary>
    IPropagatorBlock<TIn, TOut> ToPipeline();

    /// <summary>
    /// Terminates the pipeline with an action that consumes each element.
    /// </summary>
    /// <param name="action">Action to perform on each output element.</param>
    /// <param name="configure">Optional action to configure block options.</param>
    ITargetBlock<TIn> ToSink(Action<TOut> action, Action<ExecutionDataflowBlockOptions> configure = null);

    /// <summary>
    /// Terminates the pipeline with an asynchronous action.
    /// </summary>
    /// <param name="action">Async action to perform on each output element.</param>
    /// <param name="configure">Optional action to configure block options.</param>
    ITargetBlock<TIn> ToSinkAsync(Func<TOut, Task> action, Action<ExecutionDataflowBlockOptions> configure = null);

    /// <summary>
    /// Terminates the pipeline with an asynchronous action with cancellation support.
    /// </summary>
    /// <param name="action">Async action with cancellation support.</param>
    /// <param name="configure">Optional action to configure block options.</param>
    ITargetBlock<TIn> ToSinkAsync(Func<TOut, CancellationToken, Task> action, Action<ExecutionDataflowBlockOptions> configure = null);
}