using System.Threading.Tasks.Dataflow;

namespace RtFlow.Pipelines.Core;

/// <summary>
/// A central hub for accessing shared pipelines across services
/// </summary>
public interface IPipelineHub
{
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
    /// Completes all pipelines when application is shutting down
    /// </summary>
    /// <returns>A task representing the completion of all pipelines</returns>
    Task CompleteAllAsync();
}
