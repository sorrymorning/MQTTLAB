using Lab7.Mqtt.Common;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lab7.Mqtt.Publisher
{
    class Program
    {
        private static IMemoryCache memoryCache;
        private static IConfiguration configuration;
        private static ServiceProvider serviceProvider;
        private static MqttConnectionSettings MqttConnectionSettings;

        static async Task Main(string[] args)
        {
            var services = new ServiceCollection();
            services.AddMemoryCache()
                    .AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

            serviceProvider = services.BuildServiceProvider();

            memoryCache = serviceProvider.GetService<IMemoryCache>();

            configuration = InitConfiguration();

            MqttConnectionSettings = configuration.GetSection("Mqtt").Get<MqttConnectionSettings>();
            var logger = serviceProvider.GetRequiredService<ILogger<Common.Mqtt>>();

            var eventBus = new Common.Mqtt(logger, MqttConnectionSettings);

            await StartPublishAsync(eventBus);
        }

        private static async Task StartPublishAsync(Common.Mqtt mqttSender)
        {
            var topic = configuration.GetValue<string>("Topic");

            Console.WriteLine("Enter your messages (type 'exit' to quit):");

            while (true)
            {
                Console.Write("> ");
                var message = Console.ReadLine();

                if (message?.ToLower() == "exit")
                    break;

                await mqttSender.PublishAsync(topic, message);
                Console.WriteLine($"Sent: {message}");
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
