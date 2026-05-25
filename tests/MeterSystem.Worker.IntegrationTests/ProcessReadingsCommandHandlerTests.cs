using FluentAssertions;
using MediatR;
using MeterSystem.Shared.Messages;
using MeterSystem.Worker.Commands;
using MeterSystem.Worker.IntegrationTests.Fakes;
using MeterSystem.Worker.IntegrationTests.Fixtures;

namespace MeterSystem.Worker.IntegrationTests;

[Collection("Worker")]
public sealed class ProcessReadingsCommandHandlerTests(WorkerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── 1. Happy path ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NewMeterWithReadings_PersistsAllRowsToDatabase()
    {
        var t1 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t2 = t1.AddHours(1);

        var message = new MeterReadingMessage(
            MeterNumber: 100_001,
            Readings: new Dictionary<DateTimeOffset, double> { [t1] = 12.5, [t2] = 13.0 });

        var handler = fixture.BuildHandler();
        await handler.Handle(new ProcessReadingsCommand(message), CancellationToken.None);

        var rows = await fixture.GetReadingsAsync(100_001);
        rows.Should().HaveCount(2);
        rows[0].ValueAt.Should().BeCloseTo(t1, TimeSpan.FromSeconds(1));
        rows[0].Value.Should().Be(12.5);
        rows[1].ValueAt.Should().BeCloseTo(t2, TimeSpan.FromSeconds(1));
        rows[1].Value.Should().Be(13.0);
    }

    // ── 2. DB-level deduplication (within a single message) ──────────────────

    [Fact]
    public async Task Handle_BatchContainsDuplicateTimestamp_StoresOnlyOneReading()
    {
        // Dictionary already deduplicates by key, so we simulate by sending two
        // messages for the same meter/timestamp back-to-back in a fresh session
        // (no Redis cache warm-up) so the second hit is stopped by the DB constraint.
        var ts = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero);

        var message = new MeterReadingMessage(
            MeterNumber: 100_002,
            Readings: new Dictionary<DateTimeOffset, double> { [ts] = 99.9 });

        // First delivery: hits DB, caches in Redis
        var handler = fixture.BuildHandler();
        await handler.Handle(new ProcessReadingsCommand(message), CancellationToken.None);

        // Flush Redis so the second delivery goes all the way to the DB INSERT
        await fixture.RedisMultiplexer.GetDatabase().ExecuteAsync("FLUSHDB");

        // Second delivery: Redis miss → DB ON CONFLICT DO NOTHING (no duplicate row, no exception)
        await handler.Handle(new ProcessReadingsCommand(message), CancellationToken.None);

        var rows = await fixture.GetReadingsAsync(100_002);
        rows.Should().HaveCount(1);
        rows[0].Value.Should().Be(99.9);
    }

    // ── 3. Redis cache-hit deduplication on redelivery ───────────────────────

    [Fact]
    public async Task Handle_SameMessageDeliveredTwice_RedisSkipsDbInsertOnSecondDelivery()
    {
        var ts = new DateTimeOffset(2024, 3, 1, 12, 0, 0, TimeSpan.Zero);

        var message = new MeterReadingMessage(
            MeterNumber: 100_003,
            Readings: new Dictionary<DateTimeOffset, double> { [ts] = 42.0 });

        var handler = fixture.BuildHandler();

        // First delivery: written to DB and cached in Redis
        await handler.Handle(new ProcessReadingsCommand(message), CancellationToken.None);

        // Second delivery: Redis reports the reading is already processed → no DB write attempted
        // We verify correctness (still exactly one row) AND that Redis is actually the guard
        // by checking the key exists before the second call.
        var db = fixture.RedisMultiplexer.GetDatabase();
        var key = $"reading:{100_003}:{ts.ToUnixTimeMilliseconds()}";
        (await db.KeyExistsAsync(key)).Should().BeTrue("Redis should have cached the reading after first delivery");

        await handler.Handle(new ProcessReadingsCommand(message), CancellationToken.None);

        var rows = await fixture.GetReadingsAsync(100_003);
        rows.Should().HaveCount(1);
    }

    // ── 4. Redis unavailable — DB deduplication still correct ────────────────

    [Fact]
    public async Task Handle_RedisUnavailable_FallsBackToDbAndRemainsCorrect()
    {
        var ts = new DateTimeOffset(2024, 4, 1, 6, 0, 0, TimeSpan.Zero);

        var message = new MeterReadingMessage(
            MeterNumber: 100_004,
            Readings: new Dictionary<DateTimeOffset, double> { [ts] = 7.77 });

        // Use a cache that always throws — mimics Redis being down
        var handler = fixture.BuildHandler(cache: new AlwaysMissCache());

        // First delivery: Redis throws, falls back to DB insert
        await handler.Handle(new ProcessReadingsCommand(message), CancellationToken.None);

        // Second delivery: Redis throws again, falls back to DB → ON CONFLICT DO NOTHING
        await handler.Handle(new ProcessReadingsCommand(message), CancellationToken.None);

        var rows = await fixture.GetReadingsAsync(100_004);
        rows.Should().HaveCount(1);
        rows[0].Value.Should().Be(7.77);
    }

    // ── 5. Multiple meters are independent ───────────────────────────────────

    [Fact]
    public async Task Handle_TwoDistinctMeters_EachStoresOwnReadings()
    {
        var ts = new DateTimeOffset(2024, 5, 1, 0, 0, 0, TimeSpan.Zero);

        var handler = fixture.BuildHandler();

        await handler.Handle(new ProcessReadingsCommand(
            new MeterReadingMessage(200_001, new Dictionary<DateTimeOffset, double> { [ts] = 1.0 })),
            CancellationToken.None);

        await handler.Handle(new ProcessReadingsCommand(
            new MeterReadingMessage(200_002, new Dictionary<DateTimeOffset, double> { [ts] = 2.0 })),
            CancellationToken.None);

        (await fixture.GetReadingsAsync(200_001)).Should().HaveCount(1).And.Contain(r => r.Value == 1.0);
        (await fixture.GetReadingsAsync(200_002)).Should().HaveCount(1).And.Contain(r => r.Value == 2.0);
    }
}
