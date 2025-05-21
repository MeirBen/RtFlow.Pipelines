using System.Threading.Tasks.Dataflow;

namespace RtFlow.Pipelines.Core;

public interface IPipelineFactory
{
  /// <summary>
  /// Creates a new pipeline with a BufferBlock as the entry point
  /// </summary>
  /// <typeparam name="T">The type of data that flows through the pipeline</typeparam>
  /// <param name="configure">Optional action to configure the buffer block options</param>
  /// <returns>A fluent pipeline builder</returns>
  IFluentPipelineBuilder<T, T>
    Create<T>(Action<ExecutionDataflowBlockOptions> configure = null,
      CancellationToken stopping = default);

  /// <summary>
  /// Begins a pipeline with the specified propagator block
  /// </summary>
  /// <typeparam name="TIn">The input type of the pipeline</typeparam>
  /// <typeparam name="TOut">The output type of the pipeline</typeparam>
  /// <param name="head">The propagator block to use as the head of the pipeline</param>
  /// <param name="configure">Optional action to configure the block options</param>
  /// <returns>A fluent pipeline builder</returns>
  IFluentPipelineBuilder<TIn, TOut> BeginWith<TIn, TOut>(
    IPropagatorBlock<TIn, TOut> head,
    CancellationToken stopping = default);
}
