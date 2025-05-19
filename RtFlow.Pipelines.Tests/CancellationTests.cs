using System.Threading.Tasks.Dataflow;
using RtFlow.Pipelines.Core;
using Xunit;

namespace RtFlow.Pipelines.Tests
{
    public class CancellationTests
    {
        [Fact]
        public async Task Pipeline_Should_Stop_Processing_When_Cancelled()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var processingComplete = new TaskCompletionSource<bool>();
            var cancellationDetected = new TaskCompletionSource<bool>();
            var itemsProcessed = 0;
            var processingDelay = 100; // Longer delay to ensure consistent behavior
            
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
                        // Signal that cancellation was detected in transform
                        cancellationDetected.TrySetResult(true);
                        throw;
                    }
                })
                .ToSinkAsync(async (x, token) =>
                {
                    try
                    {
                        // Count items processed
                        Interlocked.Increment(ref itemsProcessed);
                        
                        // Signal that we've processed at least one item
                        if (itemsProcessed == 1)
                        {
                            processingComplete.TrySetResult(true);
                        }
                        
                        await Task.Delay(10, token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Signal that cancellation was detected
                        cancellationDetected.TrySetResult(true);
                        throw;
                    }
                });

            // Act: start sending items
            for (int i = 1; i <= 10; i++)
            {
                await pipeline.SendAsync(i);
            }
            
            // Wait for at least one item to be processed
            await processingComplete.Task.WaitAsync(TimeSpan.FromSeconds(2));
            
            // Cancel the pipeline
            cts.Cancel();
            
            // Wait for cancellation to be detected or timeout
            var cancellationTask = await Task.WhenAny(
                cancellationDetected.Task,
                Task.Delay(2000));
            
            // Assert
            Assert.Equal(cancellationDetected.Task, cancellationTask);
            
            // Check that not all 10 items were processed (due to cancellation)
            Assert.True(itemsProcessed < 10, 
                $"Expected fewer than 10 items to be processed due to cancellation, but got {itemsProcessed}");
        }

        [Fact]
        public async Task Global_Cancellation_Should_Propagate_To_All_Blocks()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var processingStarted = new TaskCompletionSource<bool>();
            var cancellationDetected = new TaskCompletionSource<bool>();
            var transformCompleted = false;
            var sinkCompleted = false;

            var pipeline = FluentPipeline
                .Create<int>(cancellationToken: cts.Token)
                // Add a transform step to test cancellation propagation
                .Transform(x => 
                {
                    // Signal that processing started
                    processingStarted.TrySetResult(true);
                    return x * 2;
                })
                // Add an async transform to test cancellation in async operations
                .TransformAsync(async (x, token) =>
                {
                    try
                    {
                        // Add a longer delay to give time to cancel
                        await Task.Delay(1000, token);
                        transformCompleted = true;
                        return $"Value: {x}";
                    }
                    catch (OperationCanceledException)
                    {
                        cancellationDetected.TrySetResult(true);
                        throw;
                    }
                })
                // Add a sink to verify cancellation reaches the end
                .ToSinkAsync(async (s, token) => 
                {
                    try
                    {
                        // This shouldn't execute if cancellation works properly
                        await Task.Delay(50, token);
                        sinkCompleted = true;
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected outcome if cancellation propagates correctly
                    }
                });

            // Act - Send an item to the pipeline
            await pipeline.SendAsync(42);
            
            // Wait for processing to start with timeout
            var startTask = await Task.WhenAny(
                processingStarted.Task,
                Task.Delay(1000));
            
            // Verify processing started
            Assert.Equal(processingStarted.Task, startTask);
            
            // Cancel before the async operations complete
            cts.Cancel();
            
            // Wait for cancellation to be detected or timeout
            var cancellationTask = await Task.WhenAny(
                cancellationDetected.Task, 
                Task.Delay(2000));

            // Assert
            Assert.Equal(cancellationDetected.Task, cancellationTask);
            Assert.False(transformCompleted, "Transform should not complete due to cancellation");
            Assert.False(sinkCompleted, "Sink should not complete due to cancellation");
        }
    }
}
