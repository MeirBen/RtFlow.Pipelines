using System.Threading.Tasks.Dataflow;

namespace RtFlow.Pipelines.Core;

/// <summary>
/// A central hub for accessing shared pipelines across services
/// </summary>
public interface IPipelineHub : IAsyncDisposable
{
    /// <summary>
    /// Checks whether a pipeline with the specified name exists
    /// </summary>
    /// <param name="pipelineName">The name of the pipeline to check</param>
    /// <returns>True if the pipeline exists, false otherwise</returns>
    bool PipelineExists(string pipelineName);
    
    /// <summary>
    /// Gets or creates a pipeline with the specified name and types
    /// </summary>
    /// <typeparam name="TIn">The input type of the pipeline</typeparam>
    /// <typeparam name="TOut">The output type of the pipeline</typeparam>
    /// <param name="pipelineName">The unique name of the pipeline</param>
    /// <returns>The pipeline instance</returns>
    IPropagatorBlock<TIn, TOut> GetPipeline<TIn, TOut>(string pipelineName);
    
    /// <summary>
    /// Gets or creates a pipeline with the specified name using the provided creation function
    /// </summary>
    /// <typeparam name="TIn">The input type of the pipeline</typeparam>
    /// <typeparam name="TOut">The output type of the pipeline</typeparam>
    /// <param name="pipelineName">The unique name of the pipeline</param>
    /// <param name="createPipeline">Function to create the pipeline if it doesn't exist</param>
    /// <returns>The pipeline instance</returns>
    IPropagatorBlock<TIn, TOut> GetOrCreatePipeline<TIn, TOut>(
        string pipelineName,
        Func<IPipelineFactory, IPropagatorBlock<TIn, TOut>> createPipeline);
        
    /// <summary>
    /// Gets a sink (target) pipeline with the specified name
    /// </summary>
    /// <typeparam name="T">The input type of the pipeline</typeparam>
    /// <param name="pipelineName">The unique name of the pipeline</param>
    /// <returns>The pipeline instance</returns>
    ITargetBlock<T> GetSinkPipeline<T>(string pipelineName);
    
    /// <summary>
    /// Gets or creates a sink pipeline with the specified name using the provided creation function
    /// </summary>
    /// <typeparam name="T">The input type of the pipeline</typeparam>
    /// <param name="pipelineName">The unique name of the pipeline</param>
    /// <param name="createSinkPipeline">Function to create the sink pipeline if it doesn't exist</param>
    /// <returns>The created sink pipeline</returns>
    ITargetBlock<T> GetOrCreateSinkPipeline<T>(
        string pipelineName,
        Func<IPipelineFactory, ITargetBlock<T>> createSinkPipeline);
        
    /// <summary>
    /// Removes a pipeline from the hub
    /// </summary>
    /// <param name="pipelineName">The name of the pipeline to remove</param>
    /// <returns>True if the pipeline was removed, false if it wasn't found</returns>
    bool RemovePipeline(string pipelineName);
    
    /// <summary>
    /// Completes all pipelines when application is shutting down
    /// </summary>
    /// <returns>A task representing the completion of all pipelines</returns>
    Task CompleteAllAsync();
}
