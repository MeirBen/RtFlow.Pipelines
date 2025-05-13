using System.Threading.Tasks.Dataflow;

namespace RtFlow.Pipelines.Core;

/// <summary>
/// Fluent, strongly-typed builder over the PipelineBuilder<TIn,TOut> DSL.
/// </summary>
public interface IFluentPipelineBuilder<TIn, TOut>
{
    IFluentPipelineBuilder<TIn, TNext> Transform<TNext>(
        Func<TOut, TNext> selector,
        ExecutionDataflowBlockOptions options = null);

    IFluentPipelineBuilder<TIn, TOut> Tap(
        Action<TOut> sideEffect);

    IFluentPipelineBuilder<TIn, TOut[]> Batch(
        int batchSize,
        GroupingDataflowBlockOptions options = null);

    IFluentPipelineBuilder<TIn, TOut> WithPostCompletionAction(
        Action<Task> action);

    IFluentPipelineBuilder<TIn, TOut> WithPostCompletionAction(
        Func<Task, Task> action);

    /// <summary>
    /// Materializes the pipeline into an IPropagatorBlock<TIn,TOut>.
    /// </summary>
    IPropagatorBlock<TIn, TOut> ToPipeline();
}