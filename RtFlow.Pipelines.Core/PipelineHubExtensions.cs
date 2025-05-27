using System.Threading.Tasks.Dataflow;

namespace RtFlow.Pipelines.Core;

/// <summary>
/// Extension methods for the PipelineHub
/// </summary>
public static class PipelineHubExtensions
{
    /// <summary>
    /// Creates a commonly used string to int pipeline in the hub
    /// </summary>
    /// <param name="hub">The pipeline hub</param>
    /// <param name="pipelineName">The name for the pipeline</param>
    /// <param name="parseFunction">Optional custom parsing function</param>
    /// <returns>The created pipeline</returns>
    public static IPropagatorBlock<string, int> CreateStringToIntPipeline(
        this IPipelineHub hub,
        string pipelineName,
        Func<string, int> parseFunction = null)
    {
        parseFunction ??= int.Parse;
        
        return hub.GetOrCreatePipeline(
            pipelineName,
            factory => factory
                .Create<string>()
                .Transform(parseFunction)
                .ToPipeline());
    }
    
    /// <summary>
    /// Creates a transformation pipeline with the specified transformation function
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="TOut">The output type</typeparam>
    /// <param name="hub">The pipeline hub</param>
    /// <param name="pipelineName">The name for the pipeline</param>
    /// <param name="transformFunction">The transformation function</param>
    /// <param name="configureOptions">Optional action to configure pipeline options</param>
    /// <returns>The created pipeline</returns>
    public static IPropagatorBlock<TIn, TOut> CreateTransformPipeline<TIn, TOut>(
        this IPipelineHub hub,
        string pipelineName,
        Func<TIn, TOut> transformFunction,
        Action<ExecutionDataflowBlockOptions> configureOptions = null)
    {
        return hub.GetOrCreatePipeline(
            pipelineName,
            factory => factory
                .Create<TIn>(configureOptions)
                .Transform(transformFunction)
                .ToPipeline());
    }
    
    /// <summary>
    /// Creates an async transformation pipeline with the specified async transformation function
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="TOut">The output type</typeparam>
    /// <param name="hub">The pipeline hub</param>
    /// <param name="pipelineName">The name for the pipeline</param>
    /// <param name="transformAsyncFunction">The async transformation function</param>
    /// <param name="configureOptions">Optional action to configure pipeline options</param>
    /// <returns>The created pipeline</returns>
    public static IPropagatorBlock<TIn, TOut> CreateTransformAsyncPipeline<TIn, TOut>(
        this IPipelineHub hub,
        string pipelineName,
        Func<TIn, CancellationToken, Task<TOut>> transformAsyncFunction,
        Action<ExecutionDataflowBlockOptions> configureOptions = null)
    {
        return hub.GetOrCreatePipeline(
            pipelineName,
            factory => factory
                .Create<TIn>(configureOptions)
                .TransformAsync(transformAsyncFunction)
                .ToPipeline());
    }
    
    /// <summary>
    /// Creates a sink pipeline that consumes items with the specified action
    /// </summary>
    /// <typeparam name="T">The input type</typeparam>
    /// <param name="hub">The pipeline hub</param>
    /// <param name="pipelineName">The name for the pipeline</param>
    /// <param name="action">The action to execute for each item</param>
    /// <param name="configureOptions">Optional action to configure pipeline options</param>
    /// <returns>The created sink pipeline</returns>
    public static ITargetBlock<T> CreateSinkPipeline<T>(
        this IPipelineHub hub,
        string pipelineName,
        Action<T> action,
        Action<ExecutionDataflowBlockOptions> configureOptions = null)
    {
        return hub.GetOrCreateSinkPipeline(
            pipelineName,
            factory => factory
                .Create<T>(configureOptions)
                .ToSink(action)
        );
    }
    
    /// <summary>
    /// Creates a sink pipeline that consumes items with the specified async action
    /// </summary>
    /// <typeparam name="T">The input type</typeparam>
    /// <param name="hub">The pipeline hub</param>
    /// <param name="pipelineName">The name for the pipeline</param>
    /// <param name="asyncAction">The async action to execute for each item</param>
    /// <param name="configureOptions">Optional action to configure pipeline options</param>
    /// <returns>The created sink pipeline</returns>
    public static ITargetBlock<T> CreateSinkAsyncPipeline<T>(
        this IPipelineHub hub,
        string pipelineName,
        Func<T, Task> asyncAction,
        Action<ExecutionDataflowBlockOptions> configureOptions = null)
    {
        return hub.GetOrCreateSinkPipeline(
            pipelineName,
            factory => factory
                .Create<T>(configureOptions)
                .ToSinkAsync(asyncAction)
        );
    }
    
    /// <summary>
    /// Creates a sink pipeline that consumes items with the specified cancelable async action
    /// </summary>
    /// <typeparam name="T">The input type</typeparam>
    /// <param name="hub">The pipeline hub</param>
    /// <param name="pipelineName">The name for the pipeline</param>
    /// <param name="asyncAction">The async action to execute for each item with cancellation support</param>
    /// <param name="configureOptions">Optional action to configure pipeline options</param>
    /// <returns>The created sink pipeline</returns>
    public static ITargetBlock<T> CreateSinkAsyncPipeline<T>(
        this IPipelineHub hub,
        string pipelineName,
        Func<T, CancellationToken, Task> asyncAction,
        Action<ExecutionDataflowBlockOptions> configureOptions = null)
    {
        return hub.GetOrCreateSinkPipeline(
            pipelineName,
            factory => factory
                .Create<T>(configureOptions)
                .ToSinkAsync(asyncAction)
        );
    }
    
    /// <summary>
    /// Creates a transform to sink pipeline that transforms data and then consumes it
    /// </summary>
    /// <typeparam name="TIn">The input type</typeparam>
    /// <typeparam name="TOut">The intermediate transformation output type</typeparam>
    /// <param name="hub">The pipeline hub</param>
    /// <param name="pipelineName">The name for the pipeline</param>
    /// <param name="transformFunction">The transformation function</param>
    /// <param name="action">The action to execute for each transformed item</param>
    /// <param name="configureOptions">Optional action to configure pipeline options</param>
    /// <returns>The created sink pipeline</returns>
    public static ITargetBlock<TIn> CreateTransformSinkPipeline<TIn, TOut>(
        this IPipelineHub hub,
        string pipelineName,
        Func<TIn, TOut> transformFunction,
        Action<TOut> action,
        Action<ExecutionDataflowBlockOptions> configureOptions = null)
    {
        return hub.GetOrCreateSinkPipeline(
            pipelineName,
            factory => factory
                .Create<TIn>(configureOptions)
                .Transform(transformFunction)
                .ToSink(action)
        );
    }
}
