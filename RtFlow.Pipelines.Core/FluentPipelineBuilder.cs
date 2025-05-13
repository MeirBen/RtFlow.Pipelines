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

    public IFluentPipelineBuilder<TIn, TOut> WithPostCompletionAction(Action<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var nextBuilder = _inner.WithPostCompletionAction(action);
        return new FluentPipelineBuilder<TIn, TOut>(nextBuilder);
    }

    public IFluentPipelineBuilder<TIn, TOut> WithPostCompletionAction(Func<Task, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var nextBuilder = _inner.WithPostCompletionAction(action);
        return new FluentPipelineBuilder<TIn, TOut>(nextBuilder);
    }

    public IPropagatorBlock<TIn, TOut> ToPipeline()
        => _inner.ToPipeline();
}