using System.Threading.Tasks.Dataflow;
using RtFlow.Pipelines.Core.Utils;

namespace RtFlow.Pipelines.Core;

internal class FluentPipelineBuilder<TIn, TOut>
    : IFluentPipelineBuilder<TIn, TOut>
{
    private readonly PipelineBuilder<TIn, TOut> _inner;

    internal FluentPipelineBuilder(PipelineBuilder<TIn, TOut> inner)
    {
        ArgumentNullException.ThrowIfNull(inner, nameof(inner));
        _inner = inner;
    }

    public IFluentPipelineBuilder<TIn, TNext> Transform<TNext>(
        Func<TOut, TNext> selector,
        ExecutionDataflowBlockOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var block = options == null
            ? new TransformBlock<TOut, TNext>(selector)
            : new TransformBlock<TOut, TNext>(selector, options);
        var nextBuilder = _inner.LinkTo(block);
        return new FluentPipelineBuilder<TIn, TNext>(nextBuilder);
    }

    public IFluentPipelineBuilder<TIn, TNext> TransformAsync<TNext>(
        Func<TOut, Task<TNext>> selector,
        ExecutionDataflowBlockOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var block = options == null
            ? new TransformBlock<TOut, TNext>(selector)
            : new TransformBlock<TOut, TNext>(selector, options);
        var nextBuilder = _inner.LinkTo(block);
        return new FluentPipelineBuilder<TIn, TNext>(nextBuilder);
    }

    public IFluentPipelineBuilder<TIn, TNext> TransformAsync<TNext>(
        Func<TOut, CancellationToken, Task<TNext>> selector,
        ExecutionDataflowBlockOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(selector);

        // Create a transform block that passes the cancellation token from the dataflow options
        var block = options == null
            ? new TransformBlock<TOut, TNext>(input => selector(input, CancellationToken.None))
            : new TransformBlock<TOut, TNext>(input => selector(input, options.CancellationToken), options);

        var nextBuilder = _inner.LinkTo(block);
        return new FluentPipelineBuilder<TIn, TNext>(nextBuilder);
    }

    public IFluentPipelineBuilder<TIn, TOut> Tap(Action<TOut> sideEffect)
    {
        ArgumentNullException.ThrowIfNull(sideEffect);
        // Pass-through transform
        return Transform(t =>
        {
            sideEffect(t);
            return t;
        });
    }

    public IFluentPipelineBuilder<TIn, TOut> TapAsync(Func<TOut, Task> sideEffect)
    {
        ArgumentNullException.ThrowIfNull(sideEffect);
        // Pass-through async transform
        return TransformAsync(async t =>
        {
            await sideEffect(t).ConfigureAwait(false);
            return t;
        });
    }

    public IFluentPipelineBuilder<TIn, TOut> TapAsync(Func<TOut, CancellationToken, Task> sideEffect)
    {
        ArgumentNullException.ThrowIfNull(sideEffect);
        // Pass-through async transform with cancellation support
        return TransformAsync(async (t, cancellationToken) =>
        {
            await sideEffect(t, cancellationToken).ConfigureAwait(false);
            return t;
        });
    }

    public IFluentPipelineBuilder<TIn, TOut[]> Batch(
        int batchSize,
        GroupingDataflowBlockOptions options = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        var block = options == null
            ? new BatchBlock<TOut>(batchSize)
            : new BatchBlock<TOut>(batchSize, options);
        var nextBuilder = _inner.LinkTo(block);
        return new FluentPipelineBuilder<TIn, TOut[]>(nextBuilder);
    }

    public ITargetBlock<TIn> ToSink(Action<TOut> action, ExecutionDataflowBlockOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        var sinkBlock = options == null
            ? new ActionBlock<TOut>(action)
            : new ActionBlock<TOut>(action, options);

        return _inner.LinkTo(sinkBlock).ToPipeline();
    }

    public ITargetBlock<TIn> ToSinkAsync(Func<TOut, Task> action, ExecutionDataflowBlockOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        var sinkBlock = options == null
            ? new ActionBlock<TOut>(action)
            : new ActionBlock<TOut>(action, options);

        return _inner.LinkTo(sinkBlock).ToPipeline();
    }

    public ITargetBlock<TIn> ToSinkAsync(Func<TOut, CancellationToken, Task> action, ExecutionDataflowBlockOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        // Create an action block that passes the cancellation token from the dataflow options
        var sinkBlock = options == null
            ? new ActionBlock<TOut>(item => action(item, CancellationToken.None))
            : new ActionBlock<TOut>(item => action(item, options.CancellationToken), options);

        return _inner.LinkTo(sinkBlock).ToPipeline();
    }

    public IPropagatorBlock<TIn, TOut> ToPipeline()
        => _inner.ToPipeline();
}