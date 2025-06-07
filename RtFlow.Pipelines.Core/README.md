# RtFlow.Pipelines.Core

[![NuGet](https://img.shields.io/nuget/v/RtFlow.Pipelines.Core.svg)](https://www.nuget.org/packages/RtFlow.Pipelines.Core)
[![Downloads](https://img.shields.io/nuget/dt/RtFlow.Pipelines.Core.svg)](https://www.nuget.org/packages/RtFlow.Pipelines.Core)
[![License](https://img.shields.io/github/license/MeirBen/RtFlow.Pipelines.svg)](https://github.com/MeirBen/RtFlow.Pipelines/blob/main/LICENSE)

**High-performance, fluent data processing pipelines for .NET** 🚀

Build complex data processing workflows with an intuitive, chainable syntax using .NET's TPL Dataflow library. Perfect for ETL operations, real-time data processing, message handling, and high-throughput scenarios.

## ✨ Key Features

- 🔗 **Fluent API** - Intuitive, chainable syntax for complex workflows
- ⚡ **High Performance** - Built on TPL Dataflow with back-pressure control  
- 🛡️ **Type-safe** - Strongly-typed pipeline stages with compile-time checking
- 🚫 **Cancellation Support** - Graceful shutdown with comprehensive cancellation
- 📦 **Batching** - Efficient bulk processing with configurable batching
- 🏗️ **Pipeline Hub** - Centralized pipeline management and sharing
- 🔍 **Observability** - Built-in monitoring and side-effect operations

## 📦 Installation

```bash
dotnet add package RtFlow.Pipelines.Core
```

## 🚀 Quick Start

```csharp
using RtFlow.Pipelines.Core;

// Create a simple data processing pipeline
var pipeline = FluentPipeline
    .Create<int>()
    .Transform(x => x * 2)           // Double each number
    .Filter(x => x > 10)             // Keep only numbers > 10
    .Batch(5)                        // Group into batches of 5
    .ToSink(batch => {               // Process each batch
        Console.WriteLine($"Batch: [{string.Join(", ", batch)}]");
    });

// Process data through the pipeline
foreach (var number in Enumerable.Range(1, 20))
{
    await pipeline.SendAsync(number);
}

pipeline.Complete();
await pipeline.Completion;
// Output: Batch: [12, 14, 16, 18, 20], Batch: [22, 24, 26, 28, 30], etc.
```

## 💡 Common Use Cases

- **ETL Pipelines** - Extract, transform, and load data workflows
- **Message Processing** - Handle high-volume message streams
- **File Processing** - Process large files with batching and parallelism  
- **Real-time Analytics** - Stream processing with aggregation
- **API Data Processing** - Transform and validate API request/response data

## 📚 Complete Documentation

For comprehensive documentation, advanced examples, and best practices:

**👉 [View Complete Documentation](https://github.com/MeirBen/RtFlow.Pipelines#readme)**

## 📄 License

RtFlow.Pipelines is released under the **MIT License**. See the [LICENSE](LICENSE) file for details.

---
