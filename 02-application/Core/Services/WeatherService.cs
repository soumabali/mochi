using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Weather display service. Post-MVP Phase G-3.
    /// Fetches weather from open-meteo.com (free, no API key).
    /// Cat mood adjusts based on weather (sunny=playful, rainy=sleepy).
    /// </summary>
    public sealed class WeatherService
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(WeatherService));

        private readonly HttpClient _http = new();
        private WeatherInfo? _current;
        private DateTime _lastFetch = DateTime.MinValue;

        /// <summary>Weather info (temperature, condition code, isDay).</summary>
        public sealed class WeatherInfo
        {
            public double Temperature { get; init; }
            public int WeatherCode { get; init; }
            public bool IsDay { get; init; }
            public string Description => CodeToDescription(WeatherCode);
            public string Mood => CodeToMood(WeatherCode);
        }

        /// <summary>Current weather or null if not fetched.</summary>
        public WeatherInfo? Current => _current;

        /// <summary>True when weather data is available.</summary>
        public bool HasWeather => _current != null;

        /// <summary>Fetch interval in minutes (default 30).</summary>
        public int FetchIntervalMinutes { get; set; } = 30;

        /// <summary>Location (latitude, longitude). Default: Jakarta.</summary>
        public (double Lat, double Lon) Location { get; set; } = (-6.2088, 106.8456);

        /// <summary>Tick — call periodically to fetch weather.</summary>
        public async Task TickAsync()
        {
            if (DateTime.Now - _lastFetch < TimeSpan.FromMinutes(FetchIntervalMinutes)) return;
            await FetchAsync();
        }

        /// <summary>Fetch weather from API.</summary>
        public async Task FetchAsync()
        {
            try
            {
                string url = $"https://api.open-meteo.com/v1/forecast?latitude={Location.Lat}&longitude={Location.Lon}&current=temperature_2m,weather_code,is_day";
                var response = await _http.GetStringAsync(url);
                var doc = JsonDocument.Parse(response);

                if (doc.RootElement.TryGetProperty("current", out var cur))
                {
                    _current = new WeatherInfo
                    {
                        Temperature = cur.GetProperty("temperature_2m").GetDouble(),
                        WeatherCode = cur.GetProperty("weather_code").GetInt32(),
                        IsDay = cur.GetProperty("is_day").GetInt32() == 1,
                    };
                    _lastFetch = DateTime.Now;
                    Logger.Information("Weather: {Temp:F1}°C, code={Code}, {Desc}",
                        _current.Temperature, _current.WeatherCode, _current.Description);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Weather fetch failed");
            }
        }

        /// <summary>Convert WMO weather code to description.</summary>
        public static string CodeToDescription(int code) => code switch
        {
            0 => "Clear sky",
            1 or 2 or 3 => "Partly cloudy",
            45 or 48 => "Foggy",
            51 or 53 or 55 => "Drizzle",
            56 or 57 => "Freezing drizzle",
            61 or 63 or 65 => "Rain",
            66 or 67 => "Freezing rain",
            71 or 73 or 75 => "Snow",
            77 => "Snow grains",
            80 or 81 or 82 => "Rain showers",
            85 or 86 => "Snow showers",
            95 => "Thunderstorm",
            96 or 99 => "Thunderstorm + hail",
            _ => "Unknown",
        };

        /// <summary>Convert weather code to cat mood suggestion.</summary>
        public static string CodeToMood(int code) => code switch
        {
            0 => "playful",      // sunny → playful
            1 or 2 or 3 => "neutral", // cloudy → neutral
            45 or 48 => "tired",  // fog → sleepy
            51 or 53 or 55 or 56 or 57 or 61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "tired", // rain → sleepy
            71 or 73 or 75 or 77 or 85 or 86 => "neutral", // snow → calm
            95 or 96 or 99 => "sad", // storm → sad
            _ => "neutral",
        };
    }
}