using System.Threading.Tasks.Dataflow;

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
    /// Event that is raised when a pipeline is created
    /// </summary>
    public event EventHandler<PipelineCreatedEventArgs> PipelineCreated;

    /// <summary>
    /// Event that is raised when a pipeline has completed processing
    /// </summary>
    public event EventHandler<PipelineCompletedEventArgs> PipelineCompleted;

    /// <summary>
    /// Event that is raised when a pipeline has faulted
    /// </summary>
    public event EventHandler<PipelineFaultedEventArgs> PipelineFaulted;

    /// <summary>
    /// Creates a new instance of the pipeline hub
    /// </summary>
    /// <param name="factory">The pipeline factory to use for creating pipelines</param>
    public PipelineHub(IPipelineFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Checks whether a pipeline with the specified name exists
    /// </summary>
    /// <param name="pipelineName">The name of the pipeline to check</param>
    /// <returns>True if the pipeline exists, false otherwise</returns>
    public bool PipelineExists(string pipelineName)
    {
        if (string.IsNullOrEmpty(pipelineName))
            throw new ArgumentException("Pipeline name cannot be null or empty", nameof(pipelineName));

        lock (_lock)
        {
            return _pipelines.ContainsKey(pipelineName);
        }
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

            // Raise PipelineCreated event
            PipelineCreated?.Invoke(this, new PipelineCreatedEventArgs(pipelineName, typeof(IPropagatorBlock<TIn, TOut>)));

            // Subscribe to pipeline completion and faulting
            MonitorPipeline(pipeline, pipelineName);

            return pipeline;
        }
    }

    /// <summary>
    /// Gets a sink (target) pipeline with the specified name
    /// </summary>
    /// <typeparam name="T">The input type of the pipeline</typeparam>
    /// <param name="pipelineName">The unique name of the pipeline</param>
    /// <returns>The pipeline instance</returns>
    public ITargetBlock<T> GetSinkPipeline<T>(string pipelineName)
    {
        if (string.IsNullOrEmpty(pipelineName))
            throw new ArgumentException("Pipeline name cannot be null or empty", nameof(pipelineName));

        lock (_lock)
        {
            // If pipeline already exists, return it
            if (_pipelines.TryGetValue(pipelineName, out var existingPipeline))
            {
                if (existingPipeline is ITargetBlock<T> typedPipeline)
                    return typedPipeline;

                throw new InvalidOperationException(
                    $"Pipeline '{pipelineName}' exists but with different types. " +
                    $"Expected {typeof(ITargetBlock<T>).Name}, " +
                    $"found {existingPipeline.GetType().Name}");
            }

            // Check if we have a definition for this pipeline
            if (_pipelineDefinitions.TryGetValue(pipelineName, out var createFunc))
            {
                var pipeline = createFunc(_factory);

                if (pipeline is ITargetBlock<T> typedPipeline)
                {
                    _pipelines[pipelineName] = typedPipeline;
                    return typedPipeline;
                }

                throw new InvalidOperationException(
                    $"Pipeline definition for '{pipelineName}' returned wrong type. " +
                    $"Expected {typeof(ITargetBlock<T>).Name}, " +
                    $"got {pipeline.GetType().Name}");
            }

            throw new InvalidOperationException(
                $"Sink pipeline '{pipelineName}' does not exist and no creation function is defined");
        }
    }

    /// <summary>
    /// Gets or creates a sink pipeline with the specified name using the provided creation function
    /// </summary>
    /// <typeparam name="T">The input type of the pipeline</typeparam>
    /// <param name="pipelineName">The unique name of the pipeline</param>
    /// <param name="createSinkPipeline">Function to create the sink pipeline if it doesn't exist</param>
    /// <returns>The created sink pipeline</returns>
    public ITargetBlock<T> GetOrCreateSinkPipeline<T>(
        string pipelineName,
        Func<IPipelineFactory, ITargetBlock<T>> createSinkPipeline)
    {
        if (string.IsNullOrEmpty(pipelineName))
            throw new ArgumentException("Pipeline name cannot be null or empty", nameof(pipelineName));

        ArgumentNullException.ThrowIfNull(createSinkPipeline);

        lock (_lock)
        {
            // If pipeline already exists, return it
            if (_pipelines.TryGetValue(pipelineName, out var existingPipeline))
            {
                if (existingPipeline is ITargetBlock<T> typedPipeline)
                    return typedPipeline;

                throw new InvalidOperationException(
                    $"Pipeline '{pipelineName}' exists but with different types. " +
                    $"Expected {typeof(ITargetBlock<T>).Name}, " +
                    $"found {existingPipeline.GetType().Name}");
            }

            // Store the pipeline creation function for later use
            _pipelineDefinitions[pipelineName] = factory => createSinkPipeline(factory);

            // Create the pipeline
            var pipeline = createSinkPipeline(_factory);
            _pipelines[pipelineName] = pipeline;

            // Raise PipelineCreated event
            PipelineCreated?.Invoke(this, new PipelineCreatedEventArgs(pipelineName, typeof(ITargetBlock<T>)));

            // Subscribe to pipeline completion and faulting
            MonitorPipeline(pipeline, pipelineName);

            return pipeline;
        }
    }

    /// <summary>
    /// Removes a pipeline from the hub
    /// </summary>
    /// <param name="pipelineName">The name of the pipeline to remove</param>
    /// <returns>True if the pipeline was removed, false if it wasn't found</returns>
    public bool RemovePipeline(string pipelineName)
    {
        if (string.IsNullOrEmpty(pipelineName))
            throw new ArgumentException("Pipeline name cannot be null or empty", nameof(pipelineName));

        lock (_lock)
        {
            bool removed = _pipelines.Remove(pipelineName);
            if (removed)
            {
                _pipelineDefinitions.Remove(pipelineName);
            }
            return removed;
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

            try
            {
                // First empty all pipelines before completing them
                foreach (var pipeline in _pipelines.Values)
                {
                    // Try to empty the pipeline to avoid hanging on completion
                    TryEmptyPipeline(pipeline);
                }

                // Now complete all pipelines
                foreach (var pipeline in _pipelines.Values)
                {
                    if (pipeline is IDataflowBlock dataflowBlock && !dataflowBlock.Completion.IsCompleted)
                    {
                        dataflowBlock.Complete();
                    }
                }

                // Wait for completion with a timeout to avoid hanging
                foreach (var pipeline in _pipelines.Values)
                {
                    if (pipeline is IDataflowBlock dataflowBlock && !dataflowBlock.Completion.IsCompleted)
                    {
                        try
                        {
                            // Use a reasonable timeout to avoid hanging indefinitely
                            bool completed = dataflowBlock.Completion.Wait(TimeSpan.FromSeconds(5));
                            if (!completed)
                            {
                                // If we timeout, log or record this (but continue with disposal)
                                System.Diagnostics.Debug.WriteLine($"Timeout waiting for pipeline completion during disposal");
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log the exception but continue with disposal
                            System.Diagnostics.Debug.WriteLine($"Error waiting for pipeline completion: {ex.Message}");
                        }
                    }
                }

                _pipelines.Clear();
                _pipelineDefinitions.Clear();
            }
            catch (Exception ex)
            {
                // Log any exceptions during disposal but don't fail
                System.Diagnostics.Debug.WriteLine($"Exception during PipelineHub disposal: {ex}");
            }
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Sets up monitoring for pipeline completion and faults
    /// </summary>
    /// <param name="pipeline">The pipeline to monitor</param>
    /// <param name="pipelineName">The name of the pipeline</param>
    private void MonitorPipeline(object pipeline, string pipelineName)
    {
        if (pipeline is IDataflowBlock dataflowBlock)
        {
            // Start an async monitoring task that won't block
            _ = MonitorPipelineAsync(dataflowBlock, pipelineName);
        }
    }

    /// <summary>
    /// Asynchronously monitors a pipeline for completion or faults
    /// </summary>
    /// <param name="dataflowBlock">The pipeline to monitor</param>
    /// <param name="pipelineName">The name of the pipeline</param>
    private async Task MonitorPipelineAsync(IDataflowBlock dataflowBlock, string pipelineName)
    {
        try
        {
            // Await the pipeline completion
            await dataflowBlock.Completion.ConfigureAwait(false);

            // If we get here without an exception, the pipeline completed normally
            PipelineCompleted?.Invoke(this, new PipelineCompletedEventArgs(pipelineName));
        }
        catch (Exception ex)
        {
            // Pipeline faulted - raise event
            PipelineFaulted?.Invoke(this, new PipelineFaultedEventArgs(
                pipelineName,
                ex));
        }
    }
    
    /// <summary>
    /// Attempts to empty a pipeline of any pending messages by draining all source blocks
    /// </summary>
    private void TryEmptyPipeline(object pipeline)
    {
        try
        {
            // First try to handle ISourceBlock<T> directly - this is the most common case
            if (pipeline is IDataflowBlock dataflowBlock)
            {
                // Extract all blocks from the pipeline using reflection (if they're accessible)
                var blocks = new List<IDataflowBlock> { dataflowBlock };
                
                // Try to access _blocks member using reflection if this is a composite block
                var blockType = pipeline.GetType();
                var blockFields = blockType.GetFields(System.Reflection.BindingFlags.Instance | 
                                                     System.Reflection.BindingFlags.NonPublic);
                
                foreach (var field in blockFields)
                {
                    if (field.FieldType == typeof(List<IDataflowBlock>) || 
                        field.FieldType == typeof(IEnumerable<IDataflowBlock>) ||
                        field.FieldType == typeof(IList<IDataflowBlock>))
                    {
                        var fieldValue = field.GetValue(pipeline);
                        if (fieldValue is IEnumerable<IDataflowBlock> fieldBlocks)
                        {
                            blocks.AddRange(fieldBlocks);
                        }
                    }
                }
                
                // Try to empty each block
                foreach (var block in blocks)
                {
                    // For generic source blocks, use TryReceive in a loop
                    Type blockInterfaceType = null;
                    
                    foreach (var iface in block.GetType().GetInterfaces())
                    {
                        if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(ISourceBlock<>))
                        {
                            blockInterfaceType = iface;
                            break;
                        }
                    }
                    
                    if (blockInterfaceType != null)
                    {
                        var genericArgType = blockInterfaceType.GetGenericArguments()[0];
                        
                        // Get the appropriate TryReceive method
                        var tryReceiveMethod = blockInterfaceType.GetMethod("TryReceive");
                        
                        if (tryReceiveMethod != null)
                        {
                                // We need to drain the block completely
                            bool hasMoreItems;
                            do
                            {
                                // Create parameters array with the output parameter 
                                var parameters = new object[] { null, null };
                                
                                // Use reflection to invoke TryReceive with null predicate
                                // The second parameter is an 'out' parameter, which is filled by the method
                                hasMoreItems = (bool)tryReceiveMethod.Invoke(block, parameters);
                            } while (hasMoreItems);
                        }
                    }
                    
                    // For blocks with a TryReceiveAll method
                    var directTryReceiveAllMethod = block.GetType().GetMethod("TryReceiveAll");
                    if (directTryReceiveAllMethod != null)
                    {
                        // Get the generic type argument to create the right collection type
                        var genericArg = block.GetType().GenericTypeArguments.FirstOrDefault();
                        if (genericArg != null)
                        {
                            var listType = typeof(List<>).MakeGenericType(genericArg);
                            var list = Activator.CreateInstance(listType);
                            directTryReceiveAllMethod.Invoke(block, new[] { list });
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore any exceptions during cleanup - we're disposing anyway
        }
    }
}
