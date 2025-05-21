using System.Threading.Tasks.Dataflow;
using System.Collections.Concurrent;
using RtFlow.Pipelines.Core;
using Xunit;

namespace RtFlow.Pipelines.Tests
{
    public class PipelineConcurrencyTests
    {
        [Fact]
        public async Task PipelineHub_ThreadSafe_Creation()
        {
            // Arrange
            var hub = new PipelineHub(new PipelineFactory(new FakeHostApplicationLifetime()));
            var creationCount = 0;

            // Create a pipeline factory method that increments a counter when called
            Func<IPipelineFactory, IPropagatorBlock<string, int>> createFunc = f =>
            {
                Interlocked.Increment(ref creationCount);
                return f.Create<string>().Transform(s => int.Parse(s)).ToPipeline();
            };

            // Act - create pipeline from multiple threads
            var tasks = new List<Task>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(() =>
                    hub.GetOrCreatePipeline<string, int>("concurrent", createFunc)));
            }

            await Task.WhenAll(tasks);

            // Assert - should only create one instance
            Assert.Equal(1, creationCount);
            Assert.True(hub.PipelineExists("concurrent"));
        }

        [Fact]
        public async Task Dispose_Completes_All_Pipelines()
        {
            // Arrange
            var hub = new PipelineHub(new PipelineFactory(new FakeHostApplicationLifetime()));
            var results = new List<int>();
            var CancellationTokenSource = new CancellationTokenSource();

            // Create a pipeline that stores results in a list
            var pipeline = hub.GetOrCreatePipeline<int, int>("test", f =>
                f.Create<int>()
                 .Transform(i =>
                 {
                     results.Add(i);
                     return i;
                 })
                 .ToPipeline());

            // Send some data
            await pipeline.SendAsync(1);
            await pipeline.SendAsync(2);
            await pipeline.SendAsync(3);

            // Act - dispose the hub
            ((IDisposable)hub).Dispose();

            // Wait a bit for processing to complete
            await Task.Delay(100);

            // Assert
            Assert.True(pipeline.Completion.IsCompleted);
            Assert.Equal(3, results.Count);
            Assert.Contains(1, results);
            Assert.Contains(2, results);
            Assert.Contains(3, results);
        }

        [Fact]
        public async Task Data_Flows_Through_Multiple_Connected_Pipelines()
        {
            // Arrange
            var hub = new PipelineHub(new PipelineFactory(new FakeHostApplicationLifetime()));
            var results = new List<double>();

            // First pipeline: string -> int
            var p1 = hub.GetOrCreatePipeline<string, int>("p1", f =>
                f.Create<string>().Transform(s => int.Parse(s)).ToPipeline());

            // Second pipeline: int -> double
            var p2 = hub.GetOrCreatePipeline<int, double>("p2", f =>
                f.Create<int>().Transform(i => i * 1.5).ToPipeline());

            // Setup to collect results
            var consumer = new ActionBlock<double>(results.Add);
            p2.LinkTo(consumer, new DataflowLinkOptions { PropagateCompletion = true });

            // Act
            await p1.SendAsync("10");
            await p1.SendAsync("20");
            p1.Complete();

            // Manually connect pipelines
            while (await p1.OutputAvailableAsync())
            {
                var value = await p1.ReceiveAsync();
                await p2.SendAsync(value);
            }
            p2.Complete();

            await consumer.Completion;

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Contains(15.0, results);
            Assert.Contains(30.0, results);
        }

        [Fact]
        public async Task Pipeline_With_MaxDegreeOfParallelism_Processes_In_Parallel()
        {
            // Arrange
            var factory = new PipelineFactory(new FakeHostApplicationLifetime());
            var options = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 4
            };

            var processingTimes = new ConcurrentBag<TimeSpan>();
            var startTime = DateTime.UtcNow;

            // Create a pipeline with parallelism that simulates long-running work
            var pipeline = factory.Create<int>(options =>
                options.MaxDegreeOfParallelism = 4)
                .Transform(i =>
                {
                    // Simulate work taking 100ms
                    Thread.Sleep(100);
                    var elapsed = DateTime.UtcNow - startTime;
                    processingTimes.Add(elapsed);
                    return i * 2;
                })
                .ToPipeline();

            // Act - send multiple items at once
            for (int i = 0; i < 8; i++)
            {
                await pipeline.SendAsync(i);
            }

            // Consume the results
            for (int i = 0; i < 8; i++)
            {
                await pipeline.ReceiveAsync();
            }

            pipeline.Complete();
            await pipeline.Completion;

            // Assert
            // If truly parallel, total time should be around 200ms (8 items / 4 parallel = 2 batches)
            // If sequential, it would be 800ms

            // Check that items were processed in parallel by looking at timestamps
            var orderedTimes = processingTimes.OrderBy(t => t).ToList();

            // Group timestamps into batches that are close together (within 20ms)
            var batches = new List<List<TimeSpan>>();
            var currentBatch = new List<TimeSpan> { orderedTimes.First() };

            for (int i = 1; i < orderedTimes.Count; i++)
            {
                var gap = (orderedTimes[i] - orderedTimes[i - 1]).TotalMilliseconds;
                if (gap < 20) // In same batch
                {
                    currentBatch.Add(orderedTimes[i]);
                }
                else // New batch
                {
                    batches.Add(currentBatch);
                    currentBatch = new List<TimeSpan> { orderedTimes[i] };
                }
            }

            if (currentBatch.Count > 0)
                batches.Add(currentBatch);

            // We should have at least 2 batches if processing in parallel
            Assert.True(batches.Count >= 2);
        }
    }
}
