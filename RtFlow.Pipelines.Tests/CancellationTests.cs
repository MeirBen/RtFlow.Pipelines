using System.Threading.Tasks.Dataflow;
using RtFlow.Pipelines.Core;
using Xunit;

namespace RtFlow.Pipelines.Tests
{
    /// <summary>
    /// Tests that verify pipeline cancellation behavior.
    /// These tests focus on how pipelines respond to cancellation tokens and requests.
    /// </summary>
    public class CancellationTests
    {
        [Fact]
        public async Task Pipeline_Should_Stop_Processing_When_Cancelled()
        {
            // Arrange
            var cts                = new CancellationTokenSource();
            var processingComplete = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancellationDetected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var itemsProcessed     = 0;
            var processingDelay    = 100;

            var pipeline = FluentPipeline
                .Create<int>(cancellationToken: cts.Token)
                .TransformAsync(async (x, token) =>
                {
                    try
                    {
                        await Task.Delay(processingDelay, token);
                        return x * 2;
                    }
                    catch (OperationCanceledException)
                    {
                        cancellationDetected.TrySetResult(true);
                        throw;
                    }
                })
                .ToSinkAsync(async (x, token) =>
                {
                    try
                    {
                        var count = Interlocked.Increment(ref itemsProcessed);
                        if (count == 1)
                            processingComplete.TrySetResult(true);
                        await Task.Delay(10, token);
                    }
                    catch (OperationCanceledException)
                    {
                        cancellationDetected.TrySetResult(true);
                        throw;
                    }
                });

            // Act: send a bunch, wait for one to complete, then cancel
            for (int i = 1; i <= 10; i++)
                await pipeline.SendAsync(i);

            // wait up to 2s for first item to land
            await processingComplete.Task.WaitAsync(TimeSpan.FromSeconds(2));

            await cts.CancelAsync();

            // wait up to 2s for the cancellation hook to fire
            await cancellationDetected.Task.WaitAsync(TimeSpan.FromSeconds(30));

            // Assert: not *all* items could have been processed
            Assert.True(itemsProcessed < 10,
                $"Expected fewer than 10 items to be processed after cancel, got {itemsProcessed}");
        }

        [Fact]
        public async Task Global_Cancellation_Should_Propagate_To_All_Blocks()
        {
            // Arrange
            var cts                = new CancellationTokenSource();
            var processingStarted  = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancellationDetected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var transformCompleted = false;
            var sinkCompleted      = false;

            var pipeline = FluentPipeline
                .Create<int>(cancellationToken: cts.Token)
                .Transform(x =>
                {
                    processingStarted.TrySetResult(true);
                    return x * 2;
                })
                .TransformAsync(async (x, token) =>
                {
                    try
                    {
                        await Task.Delay(1_000, token);
                        transformCompleted = true;
                        return $"Value: {x}";
                    }
                    catch (OperationCanceledException)
                    {
                        cancellationDetected.TrySetResult(true);
                        throw;
                    }
                })
                .ToSinkAsync(async (s, token) =>
                {
                    try
                    {
                        await Task.Delay(50, token);
                        sinkCompleted = true;
                    }
                    catch (OperationCanceledException)
                    {
                        // swallow
                    }
                });

            // Act
            await pipeline.SendAsync(42);

            // wait up to 2s for the sync-Transform to fire
            await processingStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            // now cancel
            await cts.CancelAsync();

            // wait up to 2s for the async-Transform's catch to fire
            await cancellationDetected.Task.WaitAsync(TimeSpan.FromSeconds(2));

            // Assert
            Assert.False(transformCompleted, "Transform should not complete after cancellation");
            Assert.False(sinkCompleted,      "Sink should not complete after cancellation");
        }
    }
}
