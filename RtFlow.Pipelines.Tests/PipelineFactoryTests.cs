using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Hosting;
using RtFlow.Pipelines.Core;
using Xunit;

namespace RtFlow.Pipelines.Tests
{
    public class FakeHostApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _cts = new();
        public CancellationToken ApplicationStarted   => CancellationToken.None;
        public CancellationToken ApplicationStopping => _cts.Token;
        public CancellationToken ApplicationStopped  => CancellationToken.None;
        public void StopApplication() => _cts.Cancel();
    }

    public class PipelineFactoryTests
    {
        [Fact]
        public async Task Create_Pipeline_Observes_HostStopping_Token()
        {
            // Arrange
            var lifetime = new FakeHostApplicationLifetime();
            var factory  = new PipelineFactory(lifetime);

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
            var factory  = new PipelineFactory(lifetime);

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
    }
}
