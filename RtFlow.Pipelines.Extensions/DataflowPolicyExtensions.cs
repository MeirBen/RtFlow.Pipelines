using System.Threading.Tasks.Dataflow;
using Polly;

namespace RtFlow.Pipelines.Extensions
{
    public static class DataflowPolicyExtensions
    {
        public static TransformBlock<T, T> WithExecutionOptions<T>(
            this TransformBlock<T, T> block,
            ExecutionDataflowBlockOptions opts)
        {
            // (no-op wrapper; or build directly by passing opts to the block ctor)
            return block;
        }

        public static Func<T, Task> WithRetry<T>(
            this Func<T, Task> handler,
            int retries = 3)
        {
            var policy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(retries, i => TimeSpan.FromSeconds(Math.Pow(2, i)));
            return item => policy.ExecuteAsync(() => handler(item));
        }
    }
}
