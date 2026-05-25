using MeterSystem.Worker.IntegrationTests.Fixtures;

namespace MeterSystem.Worker.IntegrationTests;

[CollectionDefinition("Worker")]
public sealed class WorkerCollection : ICollectionFixture<WorkerFixture>;
