using System.Globalization;
using MediatR;
using MeterSystem.Api.Commands;
using MeterSystem.Api.Models;
using MeterSystem.Shared.Messages;

namespace MeterSystem.Api.Endpoints;

public static class ReadingsEndpoints
{
    public static IEndpointRouteBuilder MapReadingsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/readings", HandleReadingsAsync);
        return app;
    }

    private static async Task<IResult> HandleReadingsAsync(
        MeterReadingRequest request,
        ISender sender,
        CancellationToken ct)
    {
        var error = Validate(request);
        if (error is not null)
            return Results.BadRequest(new { error });

        await sender.Send(new PublishReadingsCommand(ToMessage(request)), ct);

        return Results.Accepted();
    }

    private static string? Validate(MeterReadingRequest request)
    {
        if (request.MeterNumber <= 0)
            return "meter_number must be a positive integer";

        if (request.Readings is null || request.Readings.Count == 0)
            return "readings must not be empty";

        foreach (var (key, value) in request.Readings)
        {
            if (!DateTimeOffset.TryParse(key, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
                return $"Invalid timestamp '{key}': must be ISO 8601 format (e.g. 2026-03-18T10:00:00Z)";

            if (!double.IsFinite(value))
                return $"Invalid value for '{key}': must be a finite number";
        }

        return null;
    }

    private static MeterReadingMessage ToMessage(MeterReadingRequest request)
    {
        var readings = request.Readings!.ToDictionary(
            kvp => DateTimeOffset.Parse(kvp.Key, CultureInfo.InvariantCulture),
            kvp => kvp.Value
        );

        return new MeterReadingMessage(request.MeterNumber, readings);
    }
}
