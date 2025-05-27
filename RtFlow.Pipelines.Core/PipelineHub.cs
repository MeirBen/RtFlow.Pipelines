using System.Threading.Tasks.Dataflow;

namespace RtFlow.Pipelines.Core;

/// <summary>
/// A central hub for managing and sharing pipelines across services
/// </summary>
public class PipelineHub : IPipelineHub, IDisposable, IAsyncDisposable
{
    private readonly IPipelineFactory _factory;
    private readonly Dictionary<string, PipelineEntry> _pipelineEntries = [];
    private readonly object _lock = new();
    private bool _isDisposed;

    /// <summary>
    /// Represents an entry in the pipeline registry, containing both the instantiated pipeline and its creator function
    /// </summary>
    private class PipelineEntry
    {
        public object Instance { get; set; }
        public Func<IPipelineFactory, object> Creator { get; set; }
        public Type PipelineType { get; set; }

        public PipelineEntry(object instance = null, Func<IPipelineFactory, object> creator = null, Type pipelineType = null)
        {
            Instance = instance;
            Creator = creator;
            PipelineType = pipelineType;
        }
    }

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
            return _pipelineEntries.ContainsKey(pipelineName) && _pipelineEntries[pipelineName].Instance != null;
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
            // If pipeline already exists in our registry
            if (_pipelineEntries.TryGetValue(pipelineName, out var entry))
            {
                // If we have an instantiated pipeline
                if (entry.Instance != null)
                {
                    if (entry.Instance is IPropagatorBlock<TIn, TOut> typedPipeline)
                        return typedPipeline;

                    throw new InvalidOperationException(
                        $"Pipeline '{pipelineName}' exists but with different types. " +
                        $"Expected {typeof(IPropagatorBlock<TIn, TOut>).Name}, " +
                        $"found {entry.Instance.GetType().Name}");
                }

                // We have a creator but no instance yet
                if (entry.Creator != null)
                {
                    var pipeline = entry.Creator(_factory);

                    if (pipeline is IPropagatorBlock<TIn, TOut> typedPipeline)
                    {
                        entry.Instance = typedPipeline;
                        entry.PipelineType = typeof(IPropagatorBlock<TIn, TOut>);
                        return typedPipeline;
                    }

                    throw new InvalidOperationException(
                        $"Pipeline definition for '{pipelineName}' returned wrong type. " +
                        $"Expected {typeof(IPropagatorBlock<TIn, TOut>).Name}, " +
                        $"got {pipeline.GetType().Name}");
                }
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
            if (_pipelineEntries.TryGetValue(pipelineName, out var entry) && entry.Instance != null)
            {
                if (entry.Instance is IPropagatorBlock<TIn, TOut> typedPipeline)
                    return typedPipeline;

                throw new InvalidOperationException(
                    $"Pipeline '{pipelineName}' exists but with different types. " +
                    $"Expected {typeof(IPropagatorBlock<TIn, TOut>).Name}, " +
                    $"found {entry.Instance.GetType().Name}");
            }

            // Store the pipeline creation function and create the pipeline
            Func<IPipelineFactory, object> wrapperFunc = factory => createPipeline(factory);

            var pipeline = createPipeline(_factory);
            _pipelineEntries[pipelineName] = new PipelineEntry(
                pipeline,
                wrapperFunc,
                typeof(IPropagatorBlock<TIn, TOut>));

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
            // If pipeline entry exists
            if (_pipelineEntries.TryGetValue(pipelineName, out var entry))
            {
                // If we have an instantiated pipeline
                if (entry.Instance != null)
                {
                    if (entry.Instance is ITargetBlock<T> typedPipeline)
                        return typedPipeline;

                    throw new InvalidOperationException(
                        $"Pipeline '{pipelineName}' exists but with different types. " +
                        $"Expected {typeof(ITargetBlock<T>).Name}, " +
                        $"found {entry.Instance.GetType().Name}");
                }

                // We have a creator but no instance yet
                if (entry.Creator != null)
                {
                    var pipeline = entry.Creator(_factory);

                    if (pipeline is ITargetBlock<T> typedPipeline)
                    {
                        entry.Instance = typedPipeline;
                        entry.PipelineType = typeof(ITargetBlock<T>);
                        return typedPipeline;
                    }

                    throw new InvalidOperationException(
                        $"Pipeline definition for '{pipelineName}' returned wrong type. " +
                        $"Expected {typeof(ITargetBlock<T>).Name}, " +
                        $"got {pipeline.GetType().Name}");
                }
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
            if (_pipelineEntries.TryGetValue(pipelineName, out var entry) && entry.Instance != null)
            {
                if (entry.Instance is ITargetBlock<T> typedPipeline)
                    return typedPipeline;

                throw new InvalidOperationException(
                    $"Pipeline '{pipelineName}' exists but with different types. " +
                    $"Expected {typeof(ITargetBlock<T>).Name}, " +
                    $"found {entry.Instance.GetType().Name}");
            }

            // Create the pipeline and store it
            Func<IPipelineFactory, object> wrapperFunc = factory => createSinkPipeline(factory);

            var pipeline = createSinkPipeline(_factory);
            _pipelineEntries[pipelineName] = new PipelineEntry(
                pipeline,
                wrapperFunc,
                typeof(ITargetBlock<T>));

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
            return _pipelineEntries.Remove(pipelineName);
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
            foreach (var entry in _pipelineEntries.Values)
            {
                if (entry.Instance is IDataflowBlock dataflowBlock)
                {
                    dataflowBlock.Complete();
                }
            }

            // Collect completion tasks
            completionTasks = [.. _pipelineEntries.Values
                .Where(entry => entry.Instance != null)
                .Select(entry => entry.Instance)
                .OfType<IDataflowBlock>()
                .Select(p => p.Completion)];
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

        List<Task> completionTasks;
        const int gracefulTimeoutMs = 2000; // 2 seconds for graceful completion
        const int cancellationTimeoutMs = 2000; // 2 seconds after cancellation

        lock (_lock)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            // First, try graceful completion - mark all pipelines for completion
            foreach (var entry in _pipelineEntries.Values)
            {
                if (entry.Instance is IDataflowBlock dataflowBlock && !dataflowBlock.Completion.IsCompleted)
                {
                    dataflowBlock.Complete();
                }
            }

            // Get all pipeline completion tasks - inside lock to capture current state
            completionTasks = _pipelineEntries.Values
                .Where(entry => entry.Instance != null)
                .Select(entry => entry.Instance)
                .OfType<IDataflowBlock>()
                .Select(p => p.Completion)
                .ToList();
        }

        // Wait for all pipelines to complete processing with timeout
        if (completionTasks.Count > 0)
        {
            try
            {
                // First attempt: graceful completion with short timeout
                var gracefulCompletion = Task.WhenAll(completionTasks);
                if (gracefulCompletion.Wait(gracefulTimeoutMs))
                {
                    // Pipelines completed gracefully
                    System.Diagnostics.Debug.WriteLine("All pipelines completed gracefully");
                }
                else
                {
                    // Timeout exceeded, dispose the factory as fallback (cancels all tokens)
                    System.Diagnostics.Debug.WriteLine("Graceful completion timeout exceeded, disposing factory");

                    // Dispose the factory if it implements IDisposable to cancel all pipelines and clean up resources
                    if (_factory is IDisposable disposableFactory)
                    {
                        disposableFactory.Dispose();
                    }
                    else
                    {
                        // Fallback to just canceling the token source
                        _factory.CancellationTokenSource?.Cancel();
                    }

                    // Wait a bit more for cancellation to take effect
                    if (!gracefulCompletion.Wait(cancellationTimeoutMs))
                    {
                        System.Diagnostics.Debug.WriteLine("Some pipelines did not complete even after factory disposal");
                    }
                }
            }
            catch (AggregateException ex)
            {
                // Log or handle any pipeline completion exceptions
                // In a dispose method we don't want to throw, just log the errors
                foreach (var innerEx in ex.InnerExceptions)
                {
                    System.Diagnostics.Debug.WriteLine($"Error during pipeline completion: {innerEx.Message}");
                }
            }
        }

        // Always dispose the factory at the end to ensure complete cleanup
        if (_factory is IDisposable factoryToDispose)
        {
            factoryToDispose.Dispose();
        }

        // Clear the collections at the end
        _pipelineEntries.Clear();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously disposes all resources used by the pipeline hub,
    /// ensuring all pipelines complete their processing
    /// </summary>
    /// <returns>A task representing the async dispose operation</returns>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        List<Task> completionTasks;
        const int gracefulTimeoutMs = 2000; // 2 seconds for graceful completion
        const int cancellationTimeoutMs = 2000; // 2 seconds after cancellation

        lock (_lock)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            // First, try graceful completion - mark all pipelines for completion
            foreach (var entry in _pipelineEntries.Values)
            {
                if (entry.Instance is IDataflowBlock dataflowBlock && !dataflowBlock.Completion.IsCompleted)
                {
                    dataflowBlock.Complete();
                }
            }

            // Collect completion tasks
            completionTasks = [.. _pipelineEntries.Values
                .Where(entry => entry.Instance != null)
                .Select(entry => entry.Instance)
                .OfType<IDataflowBlock>()
                .Select(p => p.Completion)];
        }

        // Wait for all pipelines to complete processing
        if (completionTasks.Count > 0)
        {
            try
            {
                // First attempt: graceful completion with timeout
                var gracefulCompletion = Task.WhenAll(completionTasks);
                await gracefulCompletion.WaitAsync(TimeSpan.FromMilliseconds(gracefulTimeoutMs)).ConfigureAwait(false);

                // If we reach here, pipelines completed gracefully
                System.Diagnostics.Debug.WriteLine("All pipelines completed gracefully");
            }
            catch (TimeoutException)
            {
                // Timeout exceeded, dispose the factory as fallback (cancels all tokens)
                System.Diagnostics.Debug.WriteLine("Graceful completion timeout exceeded, disposing factory");

                // Dispose the factory if it implements IDisposable to cancel all pipelines and clean up resources
                if (_factory is IDisposable disposableFactory)
                {
                    disposableFactory.Dispose();
                }
                else
                {
                    // Fallback to just canceling the token source
                    _factory.CancellationTokenSource?.Cancel();
                }

                // Wait a bit more for cancellation to take effect
                try
                {
                    var gracefulCompletion = Task.WhenAll(completionTasks);
                    await gracefulCompletion.WaitAsync(TimeSpan.FromMilliseconds(cancellationTimeoutMs)).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    System.Diagnostics.Debug.WriteLine("Some pipelines did not complete even after factory disposal");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error during pipeline cancellation: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                // Log or handle any pipeline completion exceptions
                // In a dispose method we don't want to throw, just log the errors
                System.Diagnostics.Debug.WriteLine($"Error during pipeline completion: {ex.Message}");
            }
        }

        // Always dispose the factory at the end to ensure complete cleanup
        if (_factory is IDisposable factoryDisposable)
        {
            factoryDisposable.Dispose();
        }

        // Clear the collections at the end
        _pipelineEntries.Clear();

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
}
