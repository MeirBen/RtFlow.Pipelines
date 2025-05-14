using System.Threading.Tasks.Dataflow;

namespace RtFlow.Pipelines.Core;

/// <summary>
/// Defines a fluent interface for building dataflow pipelines.
/// </summary>
/// <typeparam name="TIn">The input type of the pipeline.</typeparam>
/// <typeparam name="TOut">The output type of the pipeline.</typeparam>
public interface IFluentPipelineBuilder<TIn, TOut>
{
    /// <summary>
    /// Adds a synchronous transformation step to the pipeline.
    /// </summary>
    /// <typeparam name="TNext">The type of the output from this transformation.</typeparam>
    /// <param name="selector">The function to transform each element.</param>
    /// <param name="options">Options for the transform block, or null to use defaults.</param>
    /// <returns>A builder for the next step in the pipeline.</returns>
    IFluentPipelineBuilder<TIn, TNext> Transform<TNext>(
        Func<TOut, TNext> selector, 
        ExecutionDataflowBlockOptions options = null);
    
    /// <summary>
    /// Adds an asynchronous transformation step to the pipeline.
    /// </summary>
    /// <typeparam name="TNext">The type of the output from this transformation.</typeparam>
    /// <param name="selector">The asynchronous function to transform each element.</param>
    /// <param name="options">Options for the transform block, or null to use defaults.</param>
    /// <returns>A builder for the next step in the pipeline.</returns>
    IFluentPipelineBuilder<TIn, TNext> TransformAsync<TNext>(
        Func<TOut, Task<TNext>> selector,
        ExecutionDataflowBlockOptions options = null);
    
    /// <summary>
    /// Adds an asynchronous transformation step to the pipeline with cancellation support.
    /// </summary>
    /// <typeparam name="TNext">The type of the output from this transformation.</typeparam>
    /// <param name="selector">The asynchronous function to transform each element with cancellation support.</param>
    /// <param name="options">Options for the transform block, or null to use defaults.</param>
    /// <returns>A builder for the next step in the pipeline.</returns>
    IFluentPipelineBuilder<TIn, TNext> TransformAsync<TNext>(
        Func<TOut, CancellationToken, Task<TNext>> selector,
        ExecutionDataflowBlockOptions options = null);
        
    /// <summary>
    /// Adds a side effect that doesn't change the data but performs an operation on each element.
    /// </summary>
    /// <param name="sideEffect">The action to perform on each element.</param>
    /// <returns>A builder configured with the tap operation.</returns>
    IFluentPipelineBuilder<TIn, TOut> Tap(Action<TOut> sideEffect);
    
    /// <summary>
    /// Adds a side effect that doesn't change the data but performs an asynchronous operation on each element.
    /// </summary>
    /// <param name="sideEffect">The asynchronous action to perform on each element.</param>
    /// <returns>A builder configured with the tap operation.</returns>
    IFluentPipelineBuilder<TIn, TOut> TapAsync(Func<TOut, Task> sideEffect);
    
    /// <summary>
    /// Adds a side effect that doesn't change the data but performs an asynchronous operation on each element with cancellation support.
    /// </summary>
    /// <param name="sideEffect">The asynchronous action with cancellation support to perform on each element.</param>
    /// <returns>A builder configured with the tap operation.</returns>
    IFluentPipelineBuilder<TIn, TOut> TapAsync(Func<TOut, CancellationToken, Task> sideEffect);
    
    /// <summary>
    /// Groups elements into batches of the specified size.
    /// </summary>
    /// <param name="batchSize">The size of each batch.</param>
    /// <param name="options">Options for the batch block, or null to use defaults.</param>
    /// <returns>A builder for the next step in the pipeline with arrays as output.</returns>
    IFluentPipelineBuilder<TIn, TOut[]> Batch(
        int batchSize,
        GroupingDataflowBlockOptions options = null);
    
    /// <summary>
    /// Builds and returns the pipeline as a propagator block.
    /// </summary>
    /// <returns>A propagator block that represents the entire pipeline.</returns>
    IPropagatorBlock<TIn, TOut> ToPipeline();
    
    /// <summary>
    /// Terminates the pipeline with an action that consumes each output element.
    /// </summary>
    /// <param name="action">The action to perform on each output element.</param>
    /// <param name="options">Options for the action block, or null to use defaults.</param>
    /// <returns>A target block that represents the entire pipeline.</returns>
    ITargetBlock<TIn> ToSink(Action<TOut> action, ExecutionDataflowBlockOptions options = null);
    
    /// <summary>
    /// Terminates the pipeline with an asynchronous action that consumes each output element.
    /// </summary>
    /// <param name="action">The asynchronous action to perform on each output element.</param>
    /// <param name="options">Options for the action block, or null to use defaults.</param>
    /// <returns>A target block that represents the entire pipeline.</returns>
    ITargetBlock<TIn> ToSinkAsync(Func<TOut, Task> action, ExecutionDataflowBlockOptions options = null);
    
    /// <summary>
    /// Terminates the pipeline with an asynchronous action with cancellation support that consumes each output element.
    /// </summary>
    /// <param name="action">The asynchronous action with cancellation support to perform on each output element.</param>
    /// <param name="options">Options for the action block, or null to use defaults.</param>
    /// <returns>A target block that represents the entire pipeline.</returns>
    ITargetBlock<TIn> ToSinkAsync(Func<TOut, CancellationToken, Task> action, ExecutionDataflowBlockOptions options = null);
}