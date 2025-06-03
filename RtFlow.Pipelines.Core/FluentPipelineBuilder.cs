using System.Threading.Tasks.Dataflow;
using RtFlow.Pipelines.Core.Utils;

namespace RtFlow.Pipelines.Core
{
    internal class FluentPipelineBuilder<TIn, TOut>
        : IFluentPipelineBuilder<TIn, TOut>
    {
        private readonly PipelineBuilder<TIn, TOut> _inner;
        private readonly CancellationToken _cancellationToken;

        internal FluentPipelineBuilder(PipelineBuilder<TIn, TOut> inner, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(inner, nameof(inner));
            _inner = inner;
            _cancellationToken = cancellationToken;
        }

        public IFluentPipelineBuilder<TIn, TNext> Transform<TNext>(
            Func<TOut, TNext> selector,
            Action<ExecutionDataflowBlockOptions> configure = null)
        {
            ArgumentNullException.ThrowIfNull(selector);

            var opts = new ExecutionDataflowBlockOptions
            {
                CancellationToken = _cancellationToken
            };
            configure?.Invoke(opts);

            var block = new TransformBlock<TOut, TNext>(
                input => selector(input),
                opts);

            var nextBuilder = _inner.LinkTo(block);
            return new FluentPipelineBuilder<TIn, TNext>(nextBuilder, _cancellationToken);
        }

        public IFluentPipelineBuilder<TIn, TNext> TransformAsync<TNext>(
            Func<TOut, Task<TNext>> selector,
            Action<ExecutionDataflowBlockOptions> configure = null)
        {
            ArgumentNullException.ThrowIfNull(selector);

            var opts = new ExecutionDataflowBlockOptions
            {
                CancellationToken = _cancellationToken
            };
            configure?.Invoke(opts);

            var block = new TransformBlock<TOut, TNext>(
                input => selector(input),
                opts);

            var nextBuilder = _inner.LinkTo(block);
            return new FluentPipelineBuilder<TIn, TNext>(nextBuilder, _cancellationToken);
        }

        public IFluentPipelineBuilder<TIn, TNext> TransformAsync<TNext>(
            Func<TOut, CancellationToken, Task<TNext>> selector,
            Action<ExecutionDataflowBlockOptions> configure = null)
        {
            ArgumentNullException.ThrowIfNull(selector);

            var opts = new ExecutionDataflowBlockOptions
            {
                CancellationToken = _cancellationToken
            };
            configure?.Invoke(opts);

            var block = new TransformBlock<TOut, TNext>(
                input => selector(input, opts.CancellationToken),
                opts);

            var nextBuilder = _inner.LinkTo(block);
            return new FluentPipelineBuilder<TIn, TNext>(nextBuilder, _cancellationToken);
        }

        public IFluentPipelineBuilder<TIn, TOut> Tap(
            Action<TOut> sideEffect)
        {
            ArgumentNullException.ThrowIfNull(sideEffect);
            return Transform(t =>
            {
                sideEffect(t);
                return t;
            });
        }

        public IFluentPipelineBuilder<TIn, TOut> TapAsync(
            Func<TOut, Task> sideEffect)
        {
            ArgumentNullException.ThrowIfNull(sideEffect);
            return TransformAsync(async t =>
            {
                await sideEffect(t).ConfigureAwait(false);
                return t;
            });
        }

        public IFluentPipelineBuilder<TIn, TOut> TapAsync(
            Func<TOut, CancellationToken, Task> sideEffect)
        {
            ArgumentNullException.ThrowIfNull(sideEffect);
            return TransformAsync(async (t, ct) =>
            {
                await sideEffect(t, ct).ConfigureAwait(false);
                return t;
            });
        }

        public IFluentPipelineBuilder<TIn, TOut[]> Batch(
            int batchSize,
            Action<GroupingDataflowBlockOptions> configure = null)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

            var opts = new GroupingDataflowBlockOptions
            {
                CancellationToken = _cancellationToken
            };

            // If no BoundedCapacity is specified, set it to at least batchSize
            // This is required as BatchBlock requires BoundedCapacity >= batchSize
            // BUGFIX: Without this check, the pipeline would throw ArgumentOutOfRangeException
            if (opts.BoundedCapacity < batchSize)
            {
                opts.BoundedCapacity = batchSize;
            }

            configure?.Invoke(opts);

            // Re-check after configure is called to ensure BoundedCapacity is still valid
            // This ensures that even if the user sets a smaller BoundedCapacity in the configure action,
            // we still ensure it's at least equal to batchSize to prevent exceptions
            if (opts.BoundedCapacity < batchSize)
            {
                opts.BoundedCapacity = batchSize;
            }

            var block = new BatchBlock<TOut>(batchSize, opts);
            var nextBuilder = _inner.LinkTo(block);
            return new FluentPipelineBuilder<TIn, TOut[]>(nextBuilder, _cancellationToken);
        }

        public ITargetBlock<TIn> ToSink(
            Action<TOut> action,
            Action<ExecutionDataflowBlockOptions> configure = null)
        {
            ArgumentNullException.ThrowIfNull(action);

            var opts = new ExecutionDataflowBlockOptions
            {
                CancellationToken = _cancellationToken
            };
            configure?.Invoke(opts);

            var sink = new ActionBlock<TOut>(action, opts);
            return _inner.LinkTo(sink).ToPipeline();
        }

        public ITargetBlock<TIn> ToSinkAsync(
            Func<TOut, Task> action,
            Action<ExecutionDataflowBlockOptions> configure = null)
        {
            ArgumentNullException.ThrowIfNull(action);

            var opts = new ExecutionDataflowBlockOptions
            {
                CancellationToken = _cancellationToken
            };
            configure?.Invoke(opts);

            var sink = new ActionBlock<TOut>(action, opts);
            return _inner.LinkTo(sink).ToPipeline();
        }

        public ITargetBlock<TIn> ToSinkAsync(
            Func<TOut, CancellationToken, Task> action,
            Action<ExecutionDataflowBlockOptions> configure = null)
        {
            ArgumentNullException.ThrowIfNull(action);

            var opts = new ExecutionDataflowBlockOptions
            {
                CancellationToken = _cancellationToken
            };
            configure?.Invoke(opts);

            // Create an action block that passes the cancellation token from the dataflow options
            var sinkBlock = new ActionBlock<TOut>(
            item => action(item, opts.CancellationToken),
            opts);

            return _inner.LinkTo(sinkBlock).ToPipeline();
        }

        public IPropagatorBlock<TIn, TOut> ToPipeline()
            => _inner.ToPipeline();
    }
}
