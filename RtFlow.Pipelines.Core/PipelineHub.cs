using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Hosting;

namespace RtFlow.Pipelines.Core;

/// <summary>
/// A central hub for managing and sharing pipelines across services
/// </summary>
public class PipelineHub : IPipelineHub, IDisposable
{
    private readonly IPipelineFactory _factory;
    private readonly Dictionary<string, object> _pipelines = new();
    private readonly Dictionary<string, Func<IPipelineFactory, object>> _pipelineDefinitions = new();
    private readonly object _lock = new();
    private bool _isDisposed;
    
    /// <summary>
    /// Creates a new instance of the pipeline hub
    /// </summary>
    /// <param name="factory">The pipeline factory to use for creating pipelines</param>
    public PipelineHub(IPipelineFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }
    
    /// <summary>
    /// Gets or creates a pipeline with the specified name and types
    /// </summary>
    /// <typeparam name="TIn">The input type of the pipeline</typeparam>
    /// <typeparam name="TOut">The output type of the pipeline</typeparam>
    /// <param name="pipelineName">The unique name of the pipeline</param>
    /// <returns>The pipeline instance</returns>
    /// <exception cref="InvalidOperationException">Thrown when the pipeline doesn't exist and no creation function is defined</exception>
    public IPropagatorBlock<TIn, TOut> GetPipeline<TIn, TOut>(string pipelineName)
    {
        if (string.IsNullOrEmpty(pipelineName))
            throw new ArgumentException("Pipeline name cannot be null or empty", nameof(pipelineName));
        
        lock (_lock)
        {
            // If pipeline already exists, return it
            if (_pipelines.TryGetValue(pipelineName, out var existingPipeline))
            {
                if (existingPipeline is IPropagatorBlock<TIn, TOut> typedPipeline)
                    return typedPipeline;
                
                throw new InvalidOperationException(
                    $"Pipeline '{pipelineName}' exists but with different types. " +
                    $"Expected {typeof(IPropagatorBlock<TIn, TOut>).Name}, " + 
                    $"found {existingPipeline.GetType().Name}");
            }
            
            // Check if we have a definition for this pipeline
            if (_pipelineDefinitions.TryGetValue(pipelineName, out var createFunc))
            {
                var pipeline = createFunc(_factory);
                
                if (pipeline is IPropagatorBlock<TIn, TOut> typedPipeline)
                {
                    _pipelines[pipelineName] = typedPipeline;
                    return typedPipeline;
                }
                
                throw new InvalidOperationException(
                    $"Pipeline definition for '{pipelineName}' returned wrong type. " +
                    $"Expected {typeof(IPropagatorBlock<TIn, TOut>).Name}, " + 
                    $"got {pipeline.GetType().Name}");
            }
            
            throw new InvalidOperationException(
                $"Pipeline '{pipelineName}' does not exist and no creation function is defined");
        }
    }
    
    /// <summary>
    /// Gets or creates a pipeline with the specified name using the provided creation function
    /// </summary>
    /// <typeparam name="TIn">The input type of the pipeline</typeparam>
    /// <typeparam name="TOut">The output type of the pipeline</typeparam>
    /// <param name="pipelineName">The unique name of the pipeline</param>
    /// <param name="createPipeline">Function to create the pipeline if it doesn't exist</param>
    /// <returns>The pipeline instance</returns>
    public IPropagatorBlock<TIn, TOut> GetOrCreatePipeline<TIn, TOut>(
        string pipelineName,
        Func<IPipelineFactory, IPropagatorBlock<TIn, TOut>> createPipeline)
    {
        if (string.IsNullOrEmpty(pipelineName))
            throw new ArgumentException("Pipeline name cannot be null or empty", nameof(pipelineName));

        ArgumentNullException.ThrowIfNull(createPipeline);

        lock (_lock)
        {
            // If pipeline already exists, return it
            if (_pipelines.TryGetValue(pipelineName, out var existingPipeline))
            {
                if (existingPipeline is IPropagatorBlock<TIn, TOut> typedPipeline)
                    return typedPipeline;
                
                throw new InvalidOperationException(
                    $"Pipeline '{pipelineName}' exists but with different types. " +
                    $"Expected {typeof(IPropagatorBlock<TIn, TOut>).Name}, " + 
                    $"found {existingPipeline.GetType().Name}");
            }
            
            // Store the pipeline creation function for later use
            _pipelineDefinitions[pipelineName] = factory => createPipeline(factory);
            
            // Create the pipeline
            var pipeline = createPipeline(_factory);
            _pipelines[pipelineName] = pipeline;
            return pipeline;
        }
    }
    
    /// <summary>
    /// Completes all pipelines when application is shutting down
    /// </summary>
    /// <returns>A task representing the completion of all pipelines</returns>
    public async Task CompleteAllAsync()
    {
        List<Task> completionTasks;
        
        lock (_lock)
        {
            // Complete all pipelines
            foreach (var pipeline in _pipelines.Values)
            {
                if (pipeline is IDataflowBlock dataflowBlock)
                {
                    dataflowBlock.Complete();
                }
            }
            
            // Collect completion tasks
            completionTasks = _pipelines.Values
                .OfType<IDataflowBlock>()
                .Select(p => p.Completion)
                .ToList();
        }
        
        // Wait for all pipelines to complete
        if (completionTasks.Count > 0)
        {
            await Task.WhenAll(completionTasks);
        }
    }
    
    /// <summary>
    /// Disposes all resources used by the pipeline hub
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;
            
        lock (_lock)
        {
            if (_isDisposed)
                return;
                
            _isDisposed = true;
            
            // Complete all pipelines
            foreach (var pipeline in _pipelines.Values)
            {
                if (pipeline is IDataflowBlock dataflowBlock && !dataflowBlock.Completion.IsCompleted)
                {
                    dataflowBlock.Complete();
                }
            }
            
            _pipelines.Clear();
            _pipelineDefinitions.Clear();
        }
        
        GC.SuppressFinalize(this);
    }
}
