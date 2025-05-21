using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Hosting;
using RtFlow.Pipelines.Core;
using Xunit;

namespace RtFlow.Pipelines.Tests
{
    /// <summary>
    /// A fake implementation of IHostApplicationLifetime for testing.
    /// </summary>
    public class FakeHostApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _cts = new();
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => _cts.Token;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => _cts.Cancel();
    }

    /// <summary>
    /// Service X - responsible for pipeline configuration and initialization
    /// </summary>
    public class ServiceX
    {
        private readonly IPipelineHub _pipelineHub;
        private const string PipelineName = "StringToIntPipeline";

        public ServiceX(IPipelineHub pipelineHub)
        {
            _pipelineHub = pipelineHub;

            // Initialize the pipeline if it doesn't exist
            _pipelineHub.GetOrCreatePipeline<string, int>(
                PipelineName,
                factory => factory
                    .Create<string>()
                    .Transform(s => int.Parse(s))
                    .ToPipeline());
        }

        public void CompletePipeline()
        {
            var pipeline = _pipelineHub.GetPipeline<string, int>(PipelineName);
            pipeline.Complete();
        }
    }

    /// <summary>
    /// Service Y - pushes data to the pipeline
    /// </summary>
    public class ServiceY
    {
        private readonly IPipelineHub _pipelineHub;
        private const string PipelineName = "StringToIntPipeline";

        public ServiceY(IPipelineHub pipelineHub)
        {
            _pipelineHub = pipelineHub;
        }

        public async Task PushDataAsync(string data)
        {
            // Get the shared pipeline and push data to it
            var pipeline = _pipelineHub.GetPipeline<string, int>(PipelineName);
            await pipeline.SendAsync(data);
        }
    }

    /// <summary>
    /// Service Z - consumes data from the pipeline
    /// </summary>
    public class ServiceZ
    {
        private readonly IPipelineHub _pipelineHub;
        private readonly List<int> _receivedData = new();
        private const string PipelineName = "StringToIntPipeline";

        public ServiceZ(IPipelineHub pipelineHub)
        {
            _pipelineHub = pipelineHub;
        }

        public async Task ConsumeDataAsync()
        {
            // Get the shared pipeline and consume data from it
            var pipeline = _pipelineHub.GetPipeline<string, int>(PipelineName);

            while (await pipeline.OutputAvailableAsync())
            {
                _receivedData.Add(await pipeline.ReceiveAsync());
            }
        }

        public List<int> GetReceivedData() => _receivedData;
    }

    /// <summary>
    /// Tests that verify integration with host lifecycle and factory functionality.
    /// These tests focus on how pipelines interact with the host application lifetime.
    /// </summary>
    public class PipelineFactoryTests
    {
        [Fact]
        public async Task Create_Pipeline_Observes_HostStopping_Token()
        {
            // Arrange
            var lifetime = new FakeHostApplicationLifetime();
            var factory = new PipelineFactory(lifetime);

            var pipeline = factory
                .Create<int>()
                .TransformAsync<int>(async (x, ct) =>
                {
                    await Task.Delay(Timeout.Infinite, ct);
                    return x;
                })
                .ToPipeline();

            // Act
            var sendTask = pipeline.SendAsync(123);
            lifetime.StopApplication();

            // Assert: TPL Dataflow throws TaskCanceledException on cancellation
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await Task.WhenAll(sendTask, pipeline.Completion));
        }

        [Fact]
        public async Task BeginWith_Pipeline_Observes_HostStopping_Token()
        {
            // Arrange
            var lifetime = new FakeHostApplicationLifetime();
            var factory = new PipelineFactory(lifetime);

            // Give the head block the same shutdown token
            var headOpts = new ExecutionDataflowBlockOptions
            {
                CancellationToken = lifetime.ApplicationStopping
            };
            var head = new BufferBlock<string>(headOpts);

            var pipeline = factory
                .BeginWith<string, string>(head)
                .TransformAsync<string>(async (s, ct) =>
                {
                    await Task.Delay(Timeout.Infinite, ct);
                    return s;
                })
                .ToPipeline();

            // Act
            var sendTask = pipeline.SendAsync("hello");
            lifetime.StopApplication();

            // Assert: now cancellation propagates through head and all blocks
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await Task.WhenAll(sendTask, pipeline.Completion));
        }

        [Fact]
        public async Task Pipeline_Shared_Through_Hub_Used_By_Multiple_Services()
        {
            // Arrange
            var lifetime = new FakeHostApplicationLifetime();
            var factory = new PipelineFactory(lifetime);

            // Create the pipeline hub
            var pipelineHub = new PipelineHub(factory);

            // Create services that all use the same hub
            var serviceX = new ServiceX(pipelineHub);
            var serviceY = new ServiceY(pipelineHub);
            var serviceZ = new ServiceZ(pipelineHub);

            // Act
            var consumeTask = Task.Run(async () => await serviceZ.ConsumeDataAsync());

            // Push data from Service Y
            await serviceY.PushDataAsync("123");
            await serviceY.PushDataAsync("456");
            await serviceY.PushDataAsync("789");

            // Complete the pipeline to allow the consumer to finish
            serviceX.CompletePipeline();
            await consumeTask;

            // Assert
            var receivedData = serviceZ.GetReceivedData();
            Assert.Equal(3, receivedData.Count);
            Assert.Equal(123, receivedData[0]);
            Assert.Equal(456, receivedData[1]);
            Assert.Equal(789, receivedData[2]);
        }

        [Fact]
        public async Task PipelineHub_Can_Manage_Multiple_Pipeline_Types()
        {
            // Arrange
            var lifetime = new FakeHostApplicationLifetime();
            var factory = new PipelineFactory(lifetime);
            var hub = new PipelineHub(factory);

            // Create multiple pipelines with different types
            var stringToIntPipeline = hub.GetOrCreatePipeline<string, int>(
                "StringToInt",
                f => f.Create<string>()
                    .Transform(s => int.Parse(s))
                    .ToPipeline());

            var intToDoublePipeline = hub.GetOrCreatePipeline<int, double>(
                "IntToDouble",
                f => f.Create<int>()
                    .Transform(i => i * 1.5)
                    .ToPipeline());

            var stringReverserPipeline = hub.GetOrCreatePipeline<string, string>(
                "StringReverser",
                f => f.Create<string>()
                    .Transform(s => new string(s.Reverse().ToArray()))
                    .ToPipeline());

            // Act - use all pipelines
            await stringToIntPipeline.SendAsync("42");
            await intToDoublePipeline.SendAsync(10);
            await stringReverserPipeline.SendAsync("hello");

            // Get results
            var intResult = await stringToIntPipeline.ReceiveAsync();
            var doubleResult = await intToDoublePipeline.ReceiveAsync();
            var reverseResult = await stringReverserPipeline.ReceiveAsync();

            // Complete all pipelines
            await hub.CompleteAllAsync();

            // Assert
            Assert.Equal(42, intResult);
            Assert.Equal(15.0, doubleResult);
            Assert.Equal("olleh", reverseResult);

            // Verify we get the same pipeline instance when requesting it again
            var sameStringToIntPipeline = hub.GetPipeline<string, int>("StringToInt");
            Assert.Same(stringToIntPipeline, sameStringToIntPipeline);
        }

        [Fact]
        public async Task PipelineHub_Extensions_Simplify_Pipeline_Creation()
        {
            // Arrange
            var lifetime = new FakeHostApplicationLifetime();
            var factory = new PipelineFactory(lifetime);
            var hub = new PipelineHub(factory);

            // Create pipelines using extension methods
            var stringToIntPipeline = hub.CreateStringToIntPipeline("NumberParser");

            var doubleTransformPipeline = hub.CreateTransformPipeline<double, string>(
                "DoubleFormatter",
                d => d.ToString("F2"));

            var asyncPipeline = hub.CreateTransformAsyncPipeline<string, string>(
                "AsyncUppercase",
                async (s, ct) =>
                {
                    await Task.Delay(10, ct); // Simulate async work
                    return s.ToUpper();
                });

            // Act
            await stringToIntPipeline.SendAsync("42");
            await doubleTransformPipeline.SendAsync(3.14159);
            await asyncPipeline.SendAsync("hello world");

            var intResult = await stringToIntPipeline.ReceiveAsync();
            var formattedResult = await doubleTransformPipeline.ReceiveAsync();
            var upperResult = await asyncPipeline.ReceiveAsync();

            // Assert
            Assert.Equal(42, intResult);
            Assert.Equal("3.14", formattedResult);
            Assert.Equal("HELLO WORLD", upperResult);
        }
    }
}
