using System.Threading.Tasks.Dataflow;
using RtFlow.Pipelines.Core;
using RtFlow.Pipelines.Core.Utils;

namespace RtFlow.Pipelines.Tests
{
    public class PipelineSmokeTests
    {
        [Fact]
        public async Task Million_Items_Pipeline_With_Logging_Completes_Correctly()
        {
            const int N = 15_000_000;

            // 1) define your pipeline, inserting a TransformBlock that prints each item
            var def = new PipelineDefinition<int, int>(
                "double-and-log",
                ct => PipelineBuilder
                        .BeginWith(new BufferBlock<int>(new() { CancellationToken = ct }))
                        .LinkTo(new TransformBlock<int, int>(
                            x => x * 2,
                            new ExecutionDataflowBlockOptions { CancellationToken = ct }))
                        .ToPipeline()
            );

            // 2) create it
            var pipeline = (IPropagatorBlock<int, int>)def.Create(CancellationToken.None);

            long sum = 0;
            int count = 0;

            // 3) drain outputs asynchronously, updating count & sum
            var receiver = Task.Run(async () =>
            {
                while (await DataflowBlock.OutputAvailableAsync(pipeline))
                {
                    var item = await DataflowBlock.ReceiveAsync(pipeline);
                    Interlocked.Add(ref sum, item);
                    Interlocked.Increment(ref count);
                }
            });

            // 4) push N inputs asynchronously
            for (int i = 0; i < N; i++)
            {
                await DataflowBlock.SendAsync(pipeline, i);
            }

            // 5) complete & wait
            pipeline.Complete();
            await Task.WhenAll(receiver, pipeline.Completion);

            // 6) verify count and sum of doubled values
            Assert.Equal(N, count);
            long expectedSum = (long)N * (N - 1);
            Assert.Equal(expectedSum, sum);
        }
    }
}
