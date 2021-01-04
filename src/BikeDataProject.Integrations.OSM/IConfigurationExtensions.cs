using Microsoft.Extensions.Configuration;

namespace BikeDataProject.Integrations.OSM
{
    internal static class IConfigurationExtensions
    {
        internal static (string deployTimeSettings, string envVarPrefix) GetDeployTimeSettings(this IConfigurationBuilder configurationBuilder)
        {
            // get deploy time settings if present.
            var configuration = configurationBuilder.Build();
            var deployTimeSettings = configuration["deploy-time-settings"] ?? "/var/app/config/appsettings.json";
            configurationBuilder = configurationBuilder.AddJsonFile(deployTimeSettings, true, true);

            // get environment variable prefix.
            // do this after the deploy time settings to make sure this is configurable at deploytime.
            configuration = configurationBuilder.Build();
            var envVarPrefix = configuration["env-var-prefix"] ?? "BIKEDATA_";

            return (deployTimeSettings, envVarPrefix);
        }
        
        internal static T GetValueOrDefault<T>(this IConfiguration configuration, string key, T defaultValue = default)
        {
            if (string.IsNullOrWhiteSpace(configuration[key])) return defaultValue;

            return configuration.GetValue<T>(key);
        }
    }
}