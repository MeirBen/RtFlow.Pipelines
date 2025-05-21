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
    /// Interface to represent a service that creates a pipeline.
    /// </summary>
    public interface IPipelineProvider<TIn, TOut>
    {
        IPropagatorBlock<TIn, TOut> GetPipeline();
    }

    /// <summary>
    /// Service X - defines and starts the pipeline
    /// </summary>
    public class ServiceX : IPipelineProvider<string, int>
    {
        private readonly IPropagatorBlock<string, int> _pipeline;

        public ServiceX(IPipelineFactory factory)
        {
            // Define pipeline that converts strings to integers
            _pipeline = factory
                .Create<string>()
                .Transform(s => int.Parse(s))
                .ToPipeline();
        }

        public IPropagatorBlock<string, int> GetPipeline() => _pipeline;
    }

    /// <summary>
    /// Service Y - pushes data to the pipeline
    /// </summary>
    public class ServiceY
    {
        private readonly IPropagatorBlock<string, int> _pipeline;

        public ServiceY(IPipelineProvider<string, int> pipelineProvider)
        {
            _pipeline = pipelineProvider.GetPipeline();
        }

        public async Task PushDataAsync(string data)
        {
            // Push data to the pipeline
            await _pipeline.SendAsync(data);
        }
    }

    /// <summary>
    /// Service Z - consumes data from the pipeline
    /// </summary>
    public class ServiceZ
    {
        private readonly IPropagatorBlock<string, int> _pipeline;
        private readonly List<int> _receivedData = new();

        public ServiceZ(IPipelineProvider<string, int> pipelineProvider)
        {
            _pipeline = pipelineProvider.GetPipeline();
        }

        public async Task ConsumeDataAsync()
        {
            // Consume data from the pipeline
            while (await DataflowBlock.OutputAvailableAsync(_pipeline))
            {
                _receivedData.Add(await DataflowBlock.ReceiveAsync(_pipeline));
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
        public async Task Pipeline_Created_In_ServiceX_Used_By_ServiceY_And_ServiceZ()
        {
            // Arrange
            var lifetime = new FakeHostApplicationLifetime();
            var factory = new PipelineFactory(lifetime);

            // Service X - Define and start pipeline
            var serviceX = new ServiceX(factory);

            // Service Y - Push data to pipeline
            var serviceY = new ServiceY(serviceX);

            // Service Z - Consume data from pipeline
            var serviceZ = new ServiceZ(serviceX);

            // Act
            var consumeTask = Task.Run(async () => await serviceZ.ConsumeDataAsync());

            // Push data from Service Y
            await serviceY.PushDataAsync("123");
            await serviceY.PushDataAsync("456");
            await serviceY.PushDataAsync("789");

            // Complete the pipeline to allow the consumer to finish
            serviceX.GetPipeline().Complete();
            await consumeTask;

            // Assert
            var receivedData = serviceZ.GetReceivedData();
            Assert.Equal(3, receivedData.Count);
            Assert.Equal(123, receivedData[0]);
            Assert.Equal(456, receivedData[1]);
            Assert.Equal(789, receivedData[2]);
        }
    }
}
