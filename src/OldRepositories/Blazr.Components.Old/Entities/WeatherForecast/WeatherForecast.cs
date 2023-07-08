namespace Blazr.Components;

public record WeatherForecast
{
    public DateOnly Date { get; init; } = DateOnly.FromDateTime(DateTime.Now);

    public int TemperatureC { get; init; } = 60;

    public string? Summary { get; init; } = "Testing";
}