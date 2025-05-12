using System.Threading.Tasks.Dataflow;
using RtFlow.Pipelines.Core;
using RtFlow.Pipelines.Core.Utils;

namespace RtFlow.Pipelines.Tests
{
    public class PipelineSmokeTests
    {
        [Fact]
        public async Task Million_Items_Pipeline_With_Type_Specific_API_Completes_Correctly()
        {
            const int N = 1_000_000; // Reduced from 15M for faster test execution

            // 1) Define pipeline using the factory
            var pipeline = PipelineFactory.CreatePipeline<int, int>(
                "double-values",
                ct => PipelineBuilder
                        .BeginWith(new BufferBlock<int>(new() { CancellationToken = ct }))
                        .LinkTo(new TransformBlock<int, int>(
                            x => x * 2,
                            new ExecutionDataflowBlockOptions
                            {
                                MaxDegreeOfParallelism = Environment.ProcessorCount,
                                CancellationToken = ct
                            }))
                        .ToPipeline()
            );

            // 2) Create it using the type-specific API
            var propagator = pipeline.Create(CancellationToken.None);

            long sum = 0;
            int count = 0;

            // 3) Drain outputs asynchronously, updating count & sum
            var receiver = Task.Run(async () =>
            {
                while (await DataflowBlock.OutputAvailableAsync(propagator))
                {
                    var item = await DataflowBlock.ReceiveAsync(propagator);
                    Interlocked.Add(ref sum, item);
                    Interlocked.Increment(ref count);
                }
            });

            // 4) Push N inputs asynchronously
            for (int i = 0; i < N; i++)
            {
                await DataflowBlock.SendAsync(propagator, i);
            }

            // 5) Complete & wait
            propagator.Complete();
            await Task.WhenAll(receiver, propagator.Completion);

            // 6) Verify count and sum of doubled values
            Assert.Equal(N, count);
            long expectedSum = (long)N * (N - 1);
            Assert.Equal(expectedSum, sum);
        }

        [Fact]
        public async Task Pipeline_Via_Interface_Processes_All_Items()
        {
            const int N = 100_000;

            // 1) Define pipeline using the factory
            var pipeline = PipelineFactory.CreatePipeline<int, string>(
                "int-to-string-converter",
                ct => new TransformBlock<int, string>(
                    x => $"Item {x} doubled: {x * 2}",
                    new ExecutionDataflowBlockOptions
                    {
                        MaxDegreeOfParallelism = Environment.ProcessorCount,
                        CancellationToken = ct
                    })
            );

            // 2) Create using the interface approach
            IPipelineDefinition genericPipeline = pipeline;
            var block = (IPropagatorBlock<int, string>)genericPipeline.Create(CancellationToken.None);

            int count = 0;
            List<string> results = new List<string>();

            // 3) Drain outputs asynchronously
            var receiver = Task.Run(async () =>
            {
                while (await DataflowBlock.OutputAvailableAsync(block))
                {
                    var item = await DataflowBlock.ReceiveAsync(block);
                    results.Add(item);
                    Interlocked.Increment(ref count);
                }
            });

            // 4) Push N inputs asynchronously
            for (int i = 0; i < N; i++)
            {
                await DataflowBlock.SendAsync(block, i);
            }

            // 5) Complete & wait
            block.Complete();
            await Task.WhenAll(receiver, block.Completion);

            // 6) Verify results
            Assert.Equal(N, count);
            Assert.Equal(N, results.Count);
            Assert.Contains($"Item 42 doubled: 84", results);
        }

        [Fact]
        public async Task Complex_Pipeline_With_Multiple_Stages()
        {
            const int N = 10_000;

            // 1) Define a more complex pipeline with multiple stages
            var complexPipeline = PipelineFactory.CreatePipeline<int, string>(
                name: "complex-multi-stage",
                factory: ct =>
                {
                    // Multiple stages of processing
                    return PipelineBuilder
                        .BeginWith(new BufferBlock<int>(new() { CancellationToken = ct }))
                        .LinkTo(new TransformBlock<int, double>(
                            x => Math.Sqrt(x),
                            new ExecutionDataflowBlockOptions { CancellationToken = ct }))
                        .LinkTo(new TransformBlock<double, (double value, string category)>(
                            x => (x, x < 50 ? "small" : "large"),
                            new ExecutionDataflowBlockOptions { CancellationToken = ct }))
                        .LinkTo(new TransformBlock<(double value, string category), string>(
                            item => $"{item.category}: {item.value:F2}",
                            new ExecutionDataflowBlockOptions { CancellationToken = ct }))
                        .ToPipeline();
                });

            // 2) Create it 
            var propagator = complexPipeline.Create(CancellationToken.None);

            var results = new List<string>();
            int count = 0;

            // 3) Collect results
            var receiver = Task.Run(async () =>
            {
                while (await DataflowBlock.OutputAvailableAsync(propagator))
                {
                    var item = await DataflowBlock.ReceiveAsync(propagator);
                    results.Add(item);
                    Interlocked.Increment(ref count);
                }
            });

            // 4) Send items
            for (int i = 0; i < N; i++)
            {
                await DataflowBlock.SendAsync(propagator, i);
            }

            // 5) Complete and wait
            propagator.Complete();
            await Task.WhenAll(receiver, propagator.Completion);

            // 6) Verify results
            Assert.Equal(N, count);
            Assert.Equal(N, results.Count);
            Assert.Contains("small: 3.00", results);
            Assert.Contains("small: 7.00", results);
            Assert.Contains("large: 50.00", results);
            Assert.Contains("large: 90.02", results);
        }
    }
}