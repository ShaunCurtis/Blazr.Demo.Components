/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

namespace Blazr.Components;

public class WeatherForecastEditContext
{
    protected WeatherForecast Record;
    public event EventHandler<string?>? FieldChanged;

    private DateOnly _date;
    public DateOnly Date
    {
        get => _date;
        set
        {
            if (value != _date)
            {
                _date = value;
                NotifyFieldChanged(WeatherForecastConstants.Date);
            }
        }
    }

    private int _temperatureC;
    public int TemperatureC
    {
        get => _temperatureC;
        set
        {
            if (value != _temperatureC)
            {
                _temperatureC = value;
                NotifyFieldChanged(WeatherForecastConstants.TemperatureC);
            }
        }
    }

    private string _summary;
    public string Summary
    {
        get => _summary;
        set
        {
            if (value != _summary)
            {
                _summary = value;
                NotifyFieldChanged(WeatherForecastConstants.Summary);
            }
        }
    }

    public WeatherForecastEditContext(WeatherForecast record)
    {
        this.Record = record;
        _date = record.Date;
        _temperatureC = record.TemperatureC;
        _summary = record.Summary ?? string.Empty;
    }

    public WeatherForecast CurrentRecord =>
        new WeatherForecast
        {
            Date = Date,
            TemperatureC = TemperatureC,
            Summary = Summary,
        };

    public bool IsDirty => !Record.Equals(CurrentRecord);

    public void NotifyFieldChanged(string field)
        => FieldChanged?.Invoke(null, field);
}
