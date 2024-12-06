### Пояснительная записка к проекту MQTT издателя и подписчика

#### **Цель проекта**
Цель проекта — реализация MQTT-приложений для издателя и подписчика, которые обеспечивают возможность:
- Отправлять сообщения в разных форматах.
- Принимать и корректно декодировать входящие сообщения.

Для этого были внесены изменения в код издателя, подписчика и логики публикации сообщений.

---

### **Описание изменений**

#### **1. Модификация метода `PublishAsync`**
##### Исходный код:
Метод отправлял сообщения исключительно в формате JSON.

##### Изменения:
Метод был расширен для поддержки трёх форматов:
- **JSON:** Предполагаемый основной формат для передачи сложных структур данных. Данные сериализуются в строку JSON.
- **Текст:** Удобен для отправки простых строковых сообщений.
- **Бинарный формат:** Позволяет передавать данные в виде массива байтов, например, изображения, аудиофайлы и другие бинарные данные.

##### Реализация:
```csharp
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

    // Создание и отправка сообщения
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
```

##### Зачем это нужно:
- Расширяет функциональность издателя, делая его более универсальным для различных случаев использования.
- Удобство для пользователей: сообщения можно отправлять в формате, который лучше соответствует их задачам.

---

#### **2. Изменение логики публикации сообщений (`StartPublishAsync`)**
##### Исходный код:
Издатель читал заранее подготовленные JSON-файлы и отправлял их с определённой задержкой.

##### Изменения:
Добавлена возможность отправлять сообщения в интерактивном режиме:
- Пользователь может вводить сообщения через консоль.
- Для выхода из цикла публикации достаточно ввести команду `exit`.

##### Реализация:
```csharp
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
```

##### Зачем это нужно:
- Делает приложение издателя более интерактивным и удобным для тестирования.
- Упрощает отладку и демонстрацию работы проекта.

---

#### **3. Модификация метода `Handler` (подписчик)**
##### Исходный код:
Подписчик обрабатывал входящие сообщения, предполагая, что они всегда в формате JSON.

##### Изменения:
Метод обработки сообщений был расширен для поддержки других форматов:
- **JSON:** Если сообщение успешно десериализуется, оно логируется как JSON.
- **Текст:** Если десериализация не удалась, сообщение логируется как текст.
- **Ошибка:** При других ошибках выводится сообщение об ошибке в лог.

##### Реализация:
```csharp
private static void Handler(object sender, (string Topic, object Data) obj)
{
    var (topic, data) = obj;

    string payload = data.ToString();

    try
    {
        // Попытка обработать как JSON
        var json = JsonSerializer.Deserialize<Dictionary<string, object>>(payload);
        Logger.LogInformation($"Received JSON message on topic '{topic}': {JsonSerializer.Serialize(json)}");
    }
    catch (JsonException)
    {
        // Если не JSON, обработать как текст
        Logger.LogInformation($"Received text message on topic '{topic}': {payload}");
    }
    catch (Exception ex)
    {
        // Ошибка при обработке сообщения
        Logger.LogError($"Failed to process message on topic '{topic}': {ex.Message}");
    }
}
```

##### Зачем это нужно:
- Устраняет жесткую привязку к JSON-формату.
- Обеспечивает более гибкую обработку сообщений, позволяя подписчику корректно обрабатывать текстовые и бинарные данные.

#### Работа программы
![](https://github.com/sorrymorning/MQTTLAB/blob/testCoding/%D0%A1%D0%BD%D0%B8%D0%BC%D0%BE%D0%BA%20%D1%8D%D0%BA%D1%80%D0%B0%D0%BD%D0%B0%202024-12-07%20003407.png)

