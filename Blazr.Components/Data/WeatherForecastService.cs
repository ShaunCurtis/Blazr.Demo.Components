namespace Blazr.Components.Data;

public class WeatherForecastService
{
    private List<WeatherForecast> weatherForecasts { get; set; } = new List<WeatherForecast>();

    public IEnumerable<WeatherForecast> WeatherForecasts => this.weatherForecasts;
    public event EventHandler? ListChanged;

    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    public void GetForecasts()
    {
        if (!weatherForecasts.Any())
            this.weatherForecasts =
                Enumerable.Range(1, 2).Select(index => new WeatherForecast
                {
                    Date = DateTime.Now.AddDays(index),
                    TemperatureC = Random.Shared.Next(-20, 55),
                    Summary = Summaries[Random.Shared.Next(Summaries.Length)]
                }).ToList();
    }

    public void AddRecord()
    {
        this.weatherForecasts.Add(new WeatherForecast
        {
            Date = DateTime.Now,
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        });
        ListChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteRecord()
    {
        var record = weatherForecasts.Skip(Random.Shared.Next(0, weatherForecasts.Count() - 1)).First();
        weatherForecasts.Remove(record);
        ListChanged?.Invoke(this, EventArgs.Empty);
    }
}
