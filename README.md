# RtFlow.Pipelines

[![Build Status](https://img.shields.io/github/actions/workflow/status/MeirBen/RtFlow.Pipelines/ci.yml?branch=main)](https://github.com/MeirBen/RtFlow.Pipelines/actions)  
[![NuGet](https://img.shields.io/nuget/v/RtFlow.Pipelines.Core.svg)](https://www.nuget.org/packages/RtFlow.Pipelines.Core)

RtFlow.Pipelines provides a fluent, resilient, and observable wrapper over **TPL Dataflow** for building high-throughput, in-memory stream-processing pipelines.

**Features**

* Fluent DSL – `PipelineBuilder.BeginWith(...).LinkTo(...).ToPipeline()`
* Back-pressure & cancellation via `ExecutionDataflowBlockOptions`
* Retry & circuit-breaker policies with [Polly](https://github.com/App-vNext/Polly)
* Metrics & logging hooks (`ILogger`, `System.Diagnostics.Metrics`)
* DI-friendly – register pipelines as `IPipelineDefinition`, run via `PipelineHostedService`

---

## Getting Started

### Installation
```bash
dotnet add package RtFlow.Pipelines.Core
dotnet add package RtFlow.Pipelines.Extensions
dotnet add package RtFlow.Pipelines.Hosting
```

### Quickstart
```csharp
using RtFlow.Pipelines.Core;
using System.Threading;
using System.Threading.Tasks.Dataflow;

// 1) define your pipeline
var def = new PipelineDefinition<int,int>(
    "DoubleInts",
    ct => PipelineBuilder
            .BeginWith(new BufferBlock<int>(
                new() { BoundedCapacity = 100, CancellationToken = ct }))
            .LinkTo(new TransformBlock<int,int>(x => x * 2))
            .ToPipeline()
);

// 2) create & use it
var pipeline = def.Create(CancellationToken.None);
await pipeline.SendAsync(5);
pipeline.Complete();
await pipeline.Completion;
int result = await DataflowBlock.ReceiveAsync<int>(pipeline);   // 10
```

---

## Core Concepts

| Concept | What it does |
|---------|--------------|
| **`PipelineBuilder`** | Fluent API to build a chain/graph of blocks. |
| **`PipelineDefinition<TIn,TOut>`** | Named factory that materialises a pipeline and supports graceful cancellation. |
| **`PipelineHostedService`** | Starts all registered definitions on app start and awaits completion on shutdown. |

---

## Extensions & Policies

```csharp
using RtFlow.Pipelines.Extensions;
using Polly;

// wrap a fragile handler in a retry policy
Func<RawEvent,Task<EnrichedEvent>> enrich = /* … */;
var safeEnrich = enrich.WithRetry(retries: 3);

// execution options
var block = new TransformBlock<RawEvent,EnrichedEvent>(
    safeEnrich,
    new ExecutionDataflowBlockOptions {
        MaxDegreeOfParallelism = 4,
        BoundedCapacity        = 50,
        CancellationToken      = ct
    });
```

---

## Hosting Integration

```csharp
// Program.cs / Startup.cs
services.AddSingleton<IPipelineDefinition>(sp =>
    new PipelineDefinition<Order,EnrichedOrder>(
        "OrderEnrichment",
        ct => PipelineBuilder
                .BeginWith(new BufferBlock<Order>(new() { CancellationToken = ct }))
                .LinkTo(new TransformBlock<Order,RawEvent>(ProcessRawAsync))
                .LinkTo(new TransformBlock<RawEvent,EnrichedOrder>(safeEnrich))
                .ToPipeline()
));
services.AddHostedService<PipelineHostedService>();
```

---

## Testing

A built-in smoke test pushes **1 000 000** items through a sample pipeline and checks accuracy:

```bash
dotnet test RtFlow.Pipelines.Tests
```

---

## Contributing

1. Fork & create a branch.  
2. Commit your feature/fix **with tests**.  
3. Open a PR.  

Follow the `.editorconfig`, keep the CI green, and add meaningful documentation.

---

## License

RtFlow.Pipelines is released under the **MIT License** – feel free to use, modify, and distribute!
