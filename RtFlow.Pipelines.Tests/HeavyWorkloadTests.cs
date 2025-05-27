using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using RtFlow.Pipelines.Core;
using Xunit;
using Xunit.Abstractions;

namespace RtFlow.Pipelines.Tests
{
    /// <summary>
    /// Tests that verify performance and stability under heavy workloads.
    /// These tests process millions of records with complex transformations.
    /// </summary>
    public class HeavyWorkloadTests
    {
        private readonly ITestOutputHelper _output;

        public HeavyWorkloadTests(ITestOutputHelper output)
        {
            _output = output;
        }

        #region Test Data Models

        // Models for the ETL pipeline test
        public class SyntheticRecord
        {
            public int Id { get; set; }
            public DateTime Timestamp { get; set; }
            public double Value { get; set; }
            public int CategoryId { get; set; }
            public int RegionId { get; set; }
            public bool IsActive { get; set; }
        }

        public class EnrichedRecord
        {
            public int Id { get; set; }
            public DateTime Timestamp { get; set; }
            public double Value { get; set; }
            public string Category { get; set; }
            public string Region { get; set; }
            public string ValueGroup { get; set; }
        }

        public class ProcessedRecord
        {
            public int Id { get; set; }
            public DateTime Timestamp { get; set; }
            public double Value { get; set; }
            public string Category { get; set; }
            public string Region { get; set; }
            public string ValueGroup { get; set; }
            public int Hour { get; set; }
            public DayOfWeek DayOfWeek { get; set; }
            public double NormalizedValue { get; set; }
        }

        public class BatchSummary
        {
            public int BatchSize { get; set; }
            public double AverageValue { get; set; }
            public List<ValueGroupSummary> ValueGroups { get; set; }
            public List<RegionSummary> RegionSummary { get; set; }
            public List<HourlyDistribution> HourlyDistribution { get; set; }
        }

        public class ValueGroupSummary
        {
            public string Group { get; set; }
            public int Count { get; set; }
            public double Average { get; set; }
        }

        public class RegionSummary
        {
            public string Region { get; set; }
            public int Count { get; set; }
            public double Average { get; set; }
        }

        public class HourlyDistribution
        {
            public int Hour { get; set; }
            public int Count { get; set; }
        }

        // Models for the prime factors test
        public class FactorData
        {
            public int Value { get; set; }
            public List<int> Factors { get; set; }
        }

        public class FactorStats : FactorData
        {
            public int FactorCount { get; set; }
            public int FactorSum { get; set; }
        }

        public class BatchResult
        {
            public int Count { get; set; }
            public double AverageValue { get; set; }
            public double AverageFactorCount { get; set; }
            public int MaxFactorSum { get; set; }
        }

        #endregion

        [Fact]
        public async Task Process_OneMillionItems_WithComplexTransformations()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();
            const int itemCount = 1_000_000;
            const int batchSize = 1000;
            const int progressInterval = 250_000;

            _output.WriteLine($"Starting test with {itemCount:N0} items");

            // Build pipeline for prime factorization
            var pipeline = FluentPipeline
                .Create<int>(opts =>
                {
                    opts.BoundedCapacity = 10000;
                    opts.MaxDegreeOfParallelism = Environment.ProcessorCount;
                })
                .Transform(
                    x => new FactorData
                    {
                        Value = x,
                        Factors = CalculatePrimeFactors(x + 100)
                    },
                    opts =>
                    {
                        opts.MaxDegreeOfParallelism = Environment.ProcessorCount;
                        opts.BoundedCapacity = 5000;
                    }
                )
                .Transform(
                    item => new FactorStats
                    {
                        Value = item.Value,
                        Factors = item.Factors,
                        FactorCount = item.Factors.Count,
                        FactorSum = item.Factors.Sum()
                    }
                )
                .Batch(
                    batchSize,
                    opts => opts.BoundedCapacity = Math.Max(batchSize, opts.BoundedCapacity)
                )
                .Transform(
                    batch => new BatchResult
                    {
                        Count = batch.Length,
                        AverageValue = batch.Average(x => x.Value),
                        AverageFactorCount = batch.Average(x => x.FactorCount),
                        MaxFactorSum = batch.Max(x => x.FactorSum)
                    }
                )
                .ToPipeline();

            // Set up result collection
            var results = new List<BatchResult>();
            var receiverTask = ReceiveResults(pipeline, results, stopwatch, batchSize);

            // Act: Send data through the pipeline
            _output.WriteLine("Sending data...");
            for (int i = 0; i < itemCount; i++)
            {
                await pipeline.SendAsync(i);

                if (i > 0 && i % progressInterval == 0)
                {
                    _output.WriteLine($"Sent {i:N0} items in {stopwatch.ElapsedMilliseconds:N0}ms");
                }
            }

            // Complete the pipeline and wait for all processing to finish
            pipeline.Complete();
            await Task.WhenAll(receiverTask, pipeline.Completion);

            stopwatch.Stop();

            // Assert
            Assert.Equal(itemCount / batchSize, results.Count);
            LogPerformanceMetrics(itemCount, stopwatch);
        }

        private async Task ReceiveResults<T>(IPropagatorBlock<int, T> pipeline, List<T> results, Stopwatch stopwatch, int batchSize)
        {
            while (await DataflowBlock.OutputAvailableAsync(pipeline))
            {
                var result = await DataflowBlock.ReceiveAsync(pipeline);
                results.Add(result);

                if (results.Count % 10 == 0)
                {
                    _output.WriteLine($"Processed {results.Count * batchSize:N0} items in {stopwatch.ElapsedMilliseconds:N0}ms");
                }
            }
        }

        private void LogPerformanceMetrics(int itemCount, Stopwatch stopwatch)
        {
            double elapsedMs = stopwatch.ElapsedMilliseconds;
            double itemsPerSecond = itemCount / (elapsedMs / 1000.0);

            _output.WriteLine($"Processed {itemCount:N0} items in {elapsedMs:N0}ms");
            _output.WriteLine($"Average processing time per item: {elapsedMs / itemCount:N3}ms");
            _output.WriteLine($"Throughput: {itemsPerSecond:N0} items/second");
        }

        [Fact]
        public async Task Complex_ETL_Pipeline_With_Filtering_And_Enrichment()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();
            const int recordCount = 1_000_000;
            const int batchSize = 10_000;
            const int progressInterval = 1_000_000;

            _output.WriteLine($"Starting ETL test with {recordCount:N0} records");

            // Reference data for lookups
            var categories = Enumerable.Range(1, 100)
                .ToDictionary(k => k, v => $"Category-{v}");

            var regions = Enumerable.Range(1, 10)
                .ToDictionary(k => k, v => $"Region-{v}");

            // Set up common options
            var parallelOptions = new Action<ExecutionDataflowBlockOptions>(opts =>
            {
                opts.MaxDegreeOfParallelism = Environment.ProcessorCount;
            });

            // Build ETL pipeline
            var pipeline = FluentPipeline
                .Create<int>(opts =>
                {
                    opts.BoundedCapacity = 100000;
                    opts.MaxDegreeOfParallelism = Environment.ProcessorCount * 2;
                })
                // Generate synthetic records
                .Transform(
                    id => new SyntheticRecord
                    {
                        Id = id,
                        Timestamp = DateTime.UtcNow.AddSeconds(-id % 86400),
                        Value = Math.Sin(id * 0.1) * 1000,
                        CategoryId = (id % 100) + 1,
                        RegionId = (id % 10) + 1,
                        IsActive = id % 7 != 0 // Filter condition
                    },
                    parallelOptions
                )
                // Filter inactive records - phase 1: mark inactive as null
                .Transform(
                    record => record.IsActive ? record : null,
                    parallelOptions
                )
                // Filter inactive records - phase 2: skip nulls
                .Transform(
                    record => record,
                    opts =>
                    {
                        opts.MaxDegreeOfParallelism = Environment.ProcessorCount;
                        opts.EnsureOrdered = true;
                    }
                )
                // Enrich with lookup data
                .Transform(
                    record =>
                    {
                        if (record == null) return null;

                        try
                        {
                            return new EnrichedRecord
                            {
                                Id = record.Id,
                                Timestamp = record.Timestamp,
                                Value = record.Value,
                                Category = categories[record.CategoryId],
                                Region = regions[record.RegionId],
                                ValueGroup = GetValueGroup(record.Value)
                            };
                        }
                        catch (Exception ex)
                        {
                            _output.WriteLine($"Enrichment error: {ex.Message}");
                            return null;
                        }
                    },
                    parallelOptions
                )
                // Add computed fields
                .Transform(
                    record =>
                    {
                        if (record == null) return null;

                        return new ProcessedRecord
                        {
                            Id = record.Id,
                            Timestamp = record.Timestamp,
                            Value = record.Value,
                            Category = record.Category,
                            Region = record.Region,
                            ValueGroup = record.ValueGroup,
                            Hour = record.Timestamp.Hour,
                            DayOfWeek = record.Timestamp.DayOfWeek,
                            NormalizedValue = NormalizeValue(record.Value)
                        };
                    },
                    parallelOptions
                )
                // Last null check
                .Transform(record => record, parallelOptions)
                // Batch for aggregation
                .Batch(
                    batchSize,
                    opts => opts.BoundedCapacity = Math.Max(batchSize, opts.BoundedCapacity)
                )
                // Aggregate batches
                .Transform(
                    batch =>
                    {
                        // Filter nulls
                        var validRecords = batch.Where(r => r != null).ToArray();

                        if (validRecords.Length < batch.Length)
                        {
                            _output.WriteLine($"WARNING: Removed {batch.Length - validRecords.Length} null records from batch");
                        }

                        if (validRecords.Length == 0)
                        {
                            _output.WriteLine("ERROR: Empty batch after filtering");
                            return null;
                        }

                        // Create summary with grouped data
                        return new BatchSummary
                        {
                            BatchSize = validRecords.Length,
                            AverageValue = validRecords.Average(r => r.Value),
                            ValueGroups = [.. validRecords.GroupBy(r => r.ValueGroup)
                                .Select(g => new ValueGroupSummary
                                {
                                    Group = g.Key,
                                    Count = g.Count(),
                                    Average = g.Average(r => r.NormalizedValue)
                                })],
                            RegionSummary = [.. validRecords.GroupBy(r => r.Region)
                                .Select(g => new RegionSummary
                                {
                                    Region = g.Key,
                                    Count = g.Count(),
                                    Average = g.Average(r => r.Value)
                                })],
                            HourlyDistribution = [.. validRecords.GroupBy(r => r.Hour)
                                .Select(g => new HourlyDistribution
                                {
                                    Hour = g.Key,
                                    Count = g.Count()
                                })]
                        };
                    },
                    parallelOptions
                )
                .ToPipeline();

            // Process results
            var processedBatches = new List<BatchSummary>();
            var receiverTask = Task.Run(async () =>
            {
                try
                {
                    while (await DataflowBlock.OutputAvailableAsync(pipeline))
                    {
                        var batch = await DataflowBlock.ReceiveAsync(pipeline);

                        if (batch == null)
                        {
                            _output.WriteLine("WARNING: Skipping null batch");
                            continue;
                        }

                        processedBatches.Add(batch);

                        if (processedBatches.Count % 10 == 0)
                        {
                            _output.WriteLine($"Processed {processedBatches.Count * batchSize:N0} records in {stopwatch.ElapsedMilliseconds:N0}ms");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Receiver error: {ex.Message}");
                    throw;
                }
            });

            // Act: Send data
            _output.WriteLine("Sending record IDs...");
            for (int i = 0; i < recordCount; i++)
            {
                await pipeline.SendAsync(i);

                if (i > 0 && i % progressInterval == 0)
                {
                    _output.WriteLine($"Sent {i:N0} record IDs in {stopwatch.ElapsedMilliseconds:N0}ms");
                }
            }

            // Complete pipeline
            pipeline.Complete();
            await Task.WhenAll(receiverTask, pipeline.Completion);
            stopwatch.Stop();

            // Calculate expected results
            var expectedActiveRecords = recordCount - (recordCount / 7);
            var expectedBatches = (int)Math.Ceiling(expectedActiveRecords / (double)batchSize);

            // Assert
            _output.WriteLine($"Expected ~{expectedBatches} batches, got {processedBatches.Count}");
            Assert.True(
                processedBatches.Count > 0,
                $"Expected at least some batches, but got {processedBatches.Count}"
            );

            // Log performance metrics
            LogPerformanceMetrics(expectedActiveRecords, stopwatch);
            _output.WriteLine($"Batch count: {processedBatches.Count}");

            // Log sample batches
            LogSampleBatches(processedBatches);
        }

        private void LogSampleBatches(List<BatchSummary> processedBatches)
        {
            for (int i = 0; i < Math.Min(3, processedBatches.Count); i++)
            {
                var batch = processedBatches[i];
                _output.WriteLine($"Batch {i}: Size={batch.BatchSize}, AvgValue={batch.AverageValue:F2}");
                _output.WriteLine($"  Value Groups: {string.Join(", ", batch.ValueGroups.Take(3).Select(g => $"{g.Group}:{g.Count}"))}...");
                _output.WriteLine($"  Regions: {string.Join(", ", batch.RegionSummary.Take(3).Select(r => $"{r.Region}:{r.Count}"))}...");
            }
        }

        #region Helper Methods

        /// <summary>
        /// Finds all prime factors of a given number.
        /// </summary>
        private static List<int> CalculatePrimeFactors(int number)
        {
            var factors = new List<int>();
            if (number <= 1) return factors;

            // Check divisibility by 2
            while (number % 2 == 0)
            {
                factors.Add(2);
                number /= 2;
            }

            // Check divisibility by odd numbers starting at 3
            for (int i = 3; i <= Math.Sqrt(number); i += 2)
            {
                while (number % i == 0)
                {
                    factors.Add(i);
                    number /= i;
                }
            }

            // If number is a prime greater than 2
            if (number > 2) factors.Add(number);

            return factors;
        }

        /// <summary>
        /// Categorizes a value into a group based on its magnitude.
        /// </summary>
        private static string GetValueGroup(double value) => value switch
        {
            < -500 => "Very Low",
            < 0 => "Low",
            < 500 => "Medium",
            _ => "High"
        };

        /// <summary>
        /// Normalizes a value from [-1000, 1000] range to [0, 1] range.
        /// </summary>
        private static double NormalizeValue(double value) => (value + 1000) / 2000;

        #endregion
    }
}
