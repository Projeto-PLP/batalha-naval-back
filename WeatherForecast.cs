using System.ComponentModel;

namespace batalha_naval_back;

public class WeatherForecast
{
    [Description("Data da previsão")]
    public DateOnly Date { get; set; }

    [Description("Temperatura em graus Celsius")]
    public int TemperatureC { get; set; }

    [Description("Temperatura convertida para Fahrenheit")]
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

    [Description("Resumo da temperatura")]
    public string? Summary { get; set; }
}