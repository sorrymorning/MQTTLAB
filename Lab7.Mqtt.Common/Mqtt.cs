using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using MQTTnet.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Lab7.Mqtt.Common
{
    public class Mqtt
    {
        private readonly ILogger logger;
        private IManagedMqttClient managedMqttClient;
        private readonly MqttConnectionSettings mqttSettings;

        public event EventHandler<(string Topic, object Data)> OnReceive;

        public JsonSerializerOptions JsonSerializerOptions { get; }

        public Mqtt(ILogger logger, MqttConnectionSettings mqttSettings)
        {
            this.logger = logger;
            this.mqttSettings = mqttSettings;
            JsonSerializerOptions = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                IncludeFields = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        public async Task BuildAsync()
        {
            var mqttClientOptionsBuilder = new MqttClientOptionsBuilder()
                .WithClientId(mqttSettings.UserName ?? "client1")
                .WithTcpServer(mqttSettings.Server);

            if (mqttSettings.SslProtocol != null)
            {
                SslProtocols sslProtocol;
                if (Enum.TryParse<SslProtocols>(mqttSettings.SslProtocol, out sslProtocol))
                {
                    var tlsOptions = new MqttClientTlsOptionsBuilder()
                        .UseTls()
                        .WithSslProtocols(sslProtocol);
                    X509Certificate2 rootCrt = new X509Certificate2("rootCA.crt");

                    tlsOptions.WithCertificateValidationHandler((cert) =>
                    {
                        try
                        {
                            if (cert.SslPolicyErrors == SslPolicyErrors.None)
                            {
                                return true;
                            }

                            if (cert.SslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors)
                            {
                                cert.Chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                                cert.Chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
                                cert.Chain.ChainPolicy.ExtraStore.Add(rootCrt);

                                cert.Chain.Build((X509Certificate2)rootCrt);
                                var res = cert.Chain.ChainElements.Cast<X509ChainElement>().Any(a => a.Certificate.Thumbprint == rootCrt.Thumbprint);
                                return res;
                            }
                        }
                        catch { }

                        return false;
                    });

                    mqttClientOptionsBuilder.WithTlsOptions(tlsOptions.Build());
                }

            }

            var optionsBuilder = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(mqttClientOptionsBuilder.Build());

            var options = optionsBuilder.Build();

            managedMqttClient = new MqttFactory().CreateManagedMqttClient();

            managedMqttClient.ApplicationMessageReceivedAsync += async (e) => await ManagedMqttClient_ApplicationMessageReceivedAsync(e);

            await managedMqttClient.StartAsync(options);
        }

        private Task ManagedMqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            var incommingRaw = $"{nameof(Mqtt)}: "
                  + $" ClientId = {e.ClientId}"
                  + $" + Topic = {e.ApplicationMessage.Topic}"
                  + $" + ClientId = {e.ClientId}"
                  + $" + Payload = {payload}"
                  + $" + QoS = {e.ApplicationMessage.QualityOfServiceLevel}"
                  + $" + Retain = {e.ApplicationMessage.Retain}";

            logger.LogDebug(incommingRaw);
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(payload);

            OnReceive?.Invoke(this, (e.ApplicationMessage.Topic, data));

            return Task.CompletedTask;
        }

        public async Task Subscribe(string topic, MqttQualityOfServiceLevel qualityOfServiceLevel)
        {
            await managedMqttClient.SubscribeAsync(topic, qualityOfServiceLevel);
        }

        public async Task PublishAsync(string topic, object data, string format = "json")
        {
            byte[] payload;
            switch (format.ToLower())
            {
                case "json":
                    var json = JsonSerializer.Serialize(data, JsonSerializerOptions);
                    payload = Encoding.UTF8.GetBytes(json);
                    break;
                case "text":
                    payload = Encoding.UTF8.GetBytes(data.ToString());
                    break;
                case "binary":
                    payload = data as byte[] ?? throw new ArgumentException("Data must be of type byte[] for binary format.");
                    break;
                default:
                    throw new ArgumentException($"Unsupported format: {format}");
            }

            var mqttMessage = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(mqttSettings.QOS)
                .WithRetainFlag()
                .Build();

            if (managedMqttClient == null)
            {
                await BuildAsync();
            }

            await managedMqttClient.EnqueueAsync(mqttMessage);
            logger.LogInformation($"Published message to topic '{topic}' in format '{format}'.");
        }



    }


}
