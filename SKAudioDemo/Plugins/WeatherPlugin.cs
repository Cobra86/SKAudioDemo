using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace SKAudioDemo.Plugins
{
    public class WeatherPlugin
    {
        [KernelFunction, Description("Get the current weather for a specific location")]
        public string GetCurrentWeather(
            [Description("The city and state, e.g., San Francisco, CA")] string location)
        {
            // In a real application, this would call a weather API
            // For this demo, we'll return mock data
            Random random = new Random();
            int temperature = random.Next(50, 90);

            string[] conditions = { "Sunny", "Partly Cloudy", "Cloudy", "Rainy", "Stormy" };
            string condition = conditions[random.Next(conditions.Length)];

            return $"The current weather in {location} is {temperature}°F and {condition}.";
        }

        [KernelFunction, Description("Get the weather forecast for a specific location")]
        public string GetForecast(
            [Description("The city and state, e.g., San Francisco, CA")] string location,
            [Description("Number of days for the forecast (1-7)")] int days = 3)
        {
            // In a real application, this would call a weather API
            // For this demo, we'll return mock data
            if (days < 1 || days > 7)
            {
                return "Please provide a number of days between 1 and 7.";
            }

            Random random = new Random();
            string[] conditions = { "Sunny", "Partly Cloudy", "Cloudy", "Rainy", "Stormy" };

            string forecast = $"Weather forecast for {location}:\n";

            for (int i = 0; i < days; i++)
            {
                int temperature = random.Next(50, 90);
                string condition = conditions[random.Next(conditions.Length)];

                forecast += $"Day {i + 1}: {temperature}°F and {condition}\n";
            }

            return forecast;
        }
    }
}
