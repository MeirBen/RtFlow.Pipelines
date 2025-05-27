# RtFlow.Pipelines.Core

[![NuGet](https://img.shields.io/nuget/v/RtFlow.Pipelines.Core.svg)](https://www.nuget.org/packages/RtFlow.Pipelines.Core)

**RtFlow.Pipelines.Core** is the foundational package for building high-throughput, resilient data processing pipelines using .NET's TPL Dataflow library. It provides a fluent API for creating complex data processing workflows with enterprise-grade features.

## Installation

```bash
dotnet add package RtFlow.Pipelines.Core
```

## Key Features

- **🔗 Fluent API** - Build complex data pipelines with an intuitive, chainable syntax
- **🛡️ Type-safe** - Strongly-typed pipeline stages with compile-time checking  
- **⚡ High Performance** - Built on TPL Dataflow with back-pressure and bounded capacity control
- **🚫 Cancellation Support** - Graceful pipeline shutdown with comprehensive CancellationToken integration
- **📦 Batching** - Group elements into batches for efficient bulk processing
- **🔍 Side-effects** - Add monitoring, logging, or metrics collection without changing data flow

## Quick Start

```csharp
using RtFlow.Pipelines;

// Create a simple pipeline
var pipeline = PipelineBuilder
    .Create<int>()
    .Transform(x => x * 2)
    .Filter(x => x > 10)
    .ForEach(x => Console.WriteLine($"Processed: {x}"));

// Execute the pipeline
await pipeline.ExecuteAsync(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
```

## Core Components

- **`PipelineBuilder<T>`** - Fluent builder for creating typed pipelines
- **`IPipeline<T>`** - Core pipeline interface with execution capabilities
- **`PipelineHub`** - Centralized registry for managing named pipelines
- **Transform Operations** - Map, filter, batch, and side-effect operations
- **Execution Control** - Cancellation, completion tracking, and lifecycle management

## Advanced Usage

### Batching

```csharp
var pipeline = PipelineBuilder
    .Create<int>()
    .Batch(batchSize: 10, maxWaitTime: TimeSpan.FromSeconds(5))
    .Transform(batch => ProcessBatch(batch))
    .Build();
```

### Cancellation Support

```csharp
using var cts = new CancellationTokenSource();

var pipeline = PipelineBuilder
    .Create<string>()
    .Transform(async (item, ct) => await ProcessAsync(item, ct))
    .Build();

await pipeline.ExecuteAsync(data, cts.Token);
```

### Pipeline Hub

```csharp
// Register a pipeline
PipelineHub.Register("data-processor", myPipeline);

// Retrieve and use later
var pipeline = PipelineHub.Get<DataItem>("data-processor");
await pipeline.ExecuteAsync(items);
```

## Documentation

For complete documentation, examples, and best practices, visit the [main project repository](https://github.com/MeirBen/RtFlow.Pipelines).

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/MeirBen/RtFlow.Pipelines/blob/main/LICENSE) file for details.
