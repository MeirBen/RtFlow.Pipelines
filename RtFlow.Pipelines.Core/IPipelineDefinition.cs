using System.Threading.Tasks.Dataflow;

namespace RtFlow.Pipelines.Core
{
    public interface IPipelineDefinition
    {
        string Name { get; }
        // Returns a block that, when Complete() is called, will drain gracefully
        IDataflowBlock Create(CancellationToken cancellationToken);
    }
}
