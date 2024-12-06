using MQTTnet.Protocol;

namespace Lab7.Mqtt.Common
{
    public class MqttConnectionSettings
    {
        public string Server { get; set; }
        public int? Port { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string SslProtocol { get; set; }
        public MqttQualityOfServiceLevel QOS { get; set; }
        public string rootCA { get; set; }
    }
}
