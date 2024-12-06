using Lab7.Mqtt.Common;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lab7.Mqtt.Subscriber
{
    class Program
    {
        private static IConfiguration configuration;
        private static ILogger Logger;
        private static ServiceProvider serviceProvider;
        private static MqttConnectionSettings MqttConnectionSettings;

        static async Task Main(string[] args)
        {
            var services = new ServiceCollection();
            services.AddMemoryCache()
                    .AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

            serviceProvider = services.BuildServiceProvider();


            configuration = InitConfiguration();

            MqttConnectionSettings = configuration.GetSection("Mqtt").Get<MqttConnectionSettings>();
            Logger = serviceProvider.GetRequiredService<ILogger<Common.Mqtt>>();

            var mqttFactory = new MqttFactory();

            var eventBus = new Common.Mqtt(Logger, MqttConnectionSettings);
            eventBus.OnReceive += Handler;

            await eventBus.BuildAsync();

            await SubscribeAsync(eventBus);
            Console.ReadKey();
        }

        private static async Task SubscribeAsync(Common.Mqtt eventBus)
        {
            var topic = configuration.GetValue<string>("Topic");
            await eventBus.Subscribe(topic, MqttConnectionSettings.QOS);
        }

        private static void Handler(object sender, (string Topic, object Data) obj)
        {
            var (topic, data) = obj;

            string payload = data.ToString();

            try
            {
                // Предположим, что подписчик ожидает JSON
                var json = JsonSerializer.Deserialize<Dictionary<string, object>>(payload);
                Logger.LogInformation($"Received JSON message on topic '{topic}': {JsonSerializer.Serialize(json)}");
            }
            catch (JsonException)
            {
                // Если не JSON, пробуем как текст
                Logger.LogInformation($"Received text message on topic '{topic}': {payload}");
            }
            catch (Exception ex)
            {
                // Неизвестный формат
                Logger.LogError($"Failed to process message on topic '{topic}': {ex.Message}");
            }
        }



        private static IConfiguration InitConfiguration()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();
            return config;
        }
    }
}
