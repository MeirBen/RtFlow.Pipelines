namespace RtFlow.Pipelines.Core;

/// <summary>
/// Event arguments for pipeline creation events
/// </summary>
public class PipelineCreatedEventArgs : EventArgs
{
    /// <summary>
    /// The name of the pipeline
    /// </summary>
    public string PipelineName { get; }

    /// <summary>
    /// The type of the pipeline
    /// </summary>
    public Type PipelineType { get; }

    /// <summary>
    /// Creates a new instance of the PipelineCreatedEventArgs class
    /// </summary>
    /// <param name="pipelineName">The name of the pipeline</param>
    /// <param name="pipelineType">The type of the pipeline</param>
    public PipelineCreatedEventArgs(string pipelineName, Type pipelineType)
    {
        PipelineName = pipelineName;
        PipelineType = pipelineType;
    }
}

/// <summary>
/// Event arguments for pipeline completion events
/// </summary>
public class PipelineCompletedEventArgs : EventArgs
{
    /// <summary>
    /// The name of the pipeline
    /// </summary>
    public string PipelineName { get; }

    /// <summary>
    /// Creates a new instance of the PipelineCompletedEventArgs class
    /// </summary>
    /// <param name="pipelineName">The name of the pipeline</param>
    public PipelineCompletedEventArgs(string pipelineName)
    {
        PipelineName = pipelineName;
    }
}

/// <summary>
/// Event arguments for pipeline fault events
/// </summary>
public class PipelineFaultedEventArgs : EventArgs
{
    /// <summary>
    /// The name of the pipeline
    /// </summary>
    public string PipelineName { get; }

    /// <summary>
    /// The exception that caused the fault
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Creates a new instance of the PipelineFaultedEventArgs class
    /// </summary>
    /// <param name="pipelineName">The name of the pipeline</param>
    /// <param name="exception">The exception that caused the fault</param>
    public PipelineFaultedEventArgs(string pipelineName, Exception exception)
    {
        PipelineName = pipelineName;
        Exception = exception;
    }
}
