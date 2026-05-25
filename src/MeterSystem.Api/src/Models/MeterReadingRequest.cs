using System.Text.Json.Serialization;

namespace MeterSystem.Api.Models;

public record MeterReadingRequest(
    [property: JsonPropertyName("meter_number")] long MeterNumber,
    [property: JsonPropertyName("readings")] Dictionary<string, double>? Readings
);
