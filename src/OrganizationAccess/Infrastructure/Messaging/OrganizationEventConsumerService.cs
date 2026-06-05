using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using MediatR;
using Microsoft.Extensions.Options;

using SupportHub.OrganizationAccesss.Features.Events;

namespace SupportHub.OrganizationAccesss.Infrastructure.Messaging
{
    /// <summary>
    /// Background service that consumes events from Kafka topics, deserializes them, and routes them to the appropriate MediatR commands for processing.
    /// Designed to run continuously as part of the application's hosted services.
    /// </summary>
    public class OrganizationEventConsumerService : BackgroundService
    {
        private readonly KafkaSettings _settings;
        private readonly ILogger<OrganizationEventConsumerService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IProducer<Null, string> _dlqProducer;

        /// <summary>
        /// Initializes a new instance of the TicketEventConsumerService with the given Kafka settings, logger, and service scope factory.
        /// Also initializes a dedicated Kafka producer for sending messages to the Dead Letter Queue in case of processing failures.
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="logger"></param>
        /// <param name="scopeFactory"></param>
        public OrganizationEventConsumerService(
            IOptions<KafkaSettings> settings,
            ILogger<OrganizationEventConsumerService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _settings = settings.Value;
            _logger = logger;
            _scopeFactory = scopeFactory;

            _dlqProducer = new ProducerBuilder<Null, string>(new ProducerConfig 
            { 
                BootstrapServers = _settings.BootstrapServers,
                Acks = Acks.All
            }).Build();
        }

        /// <summary>
        /// Continuously listens to the configured Kafka topics for new events.
        /// </summary>
        /// <param name="stoppingToken">Cancellation token that signals when the service should stop.</param>
        /// <returns>/returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //Create topics before subscribing
            await CreateTopicsAsync(stoppingToken);

            //Configure the Kafka consumer with the provided settings
            var config = new ConsumerConfig
            {
                BootstrapServers = _settings.BootstrapServers,
                GroupId = _settings.GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false, //Manual commit after processing ensures no dropped messages
                AllowAutoCreateTopics = true //Allow the consumer to create topics if they don't exist
            };

            //Create the Kafka consumer instance and subscribe to the configured topics
            using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
            consumer.Subscribe(_settings.SubscribeTopics);

            _logger.LogInformation("Started listening to Kafka topics: {Topics}", string.Join(", ", _settings.SubscribeTopics));

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        //Consume a message from Kafka -> this call will block until a message is available or the stopping token is triggered
                        var consumeResult = consumer.Consume(stoppingToken);
                        var messageJson = consumeResult.Message.Value;
                        string eventType = "Unknown";

                        try
                        {
                            //Parse the message JSON to extract the event type from the metadata for routing purposes
                            using var document = JsonDocument.Parse(messageJson);
                            eventType = document.RootElement.GetProperty("metadata").GetProperty("eventType").GetString() ?? "Unknown";

                            _logger.LogInformation("Received event: {EventType}", eventType);

                            //Process the event
                            await ProcessEventAsync(eventType, messageJson, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing event of type {EventType}. Routing to DLQ.", eventType);

                            //If any exception occurs during processing, send the original message to the Dead Letter Queue for later analysis
                            await RouteToDeadLetterQueueAsync(messageJson, ex, stoppingToken);
                        }

                        //Commit the offset after successful processing to avoid re-processing on restart
                        consumer.Commit(consumeResult);
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Kafka Consume Error");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Kafka Consumer stopping due to application shutdown.");
            }
            finally
            {
                //Once the stopping token is triggered, close the consumer to release resources and leave the consumer group gracefully
                consumer.Close();

                //Flush and dispose the DLQ producer to ensure all messages are sent before shutdown
                _dlqProducer.Flush(TimeSpan.FromSeconds(5));
                _dlqProducer.Dispose();
            }
        }

        /// <summary>
        /// Creates the necessary Kafka topics if they do not already exist.
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        private async Task CreateTopicsAsync(CancellationToken stoppingToken)
        {
            try
            {
                //Use the Kafka AdminClient to check for existing topics and create any that are missing
                using var adminClient = new AdminClientBuilder(
                    new AdminClientConfig { BootstrapServers = _settings.BootstrapServers }
                ).Build();

                //Fetch the existing topics from the Kafka cluster
                var topicsMetadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
                var existingTopics = topicsMetadata.Topics.Select(t => t.Topic).ToHashSet();

                //Determine which topics from the configuration need to be created by comparing against existing topics
                var topicsToCreate = _settings.SubscribeTopics
                    .Where(topic => !existingTopics.Contains(topic))
                    .Select(topic => new TopicSpecification 
                    { 
                        Name = topic, 
                        NumPartitions = 1, 
                        ReplicationFactor = 1 
                    })
                    .ToList();

                //If there are any topics that need to be created, do so
                if (topicsToCreate.Count > 0)
                {
                    await adminClient.CreateTopicsAsync(topicsToCreate);
                    _logger.LogInformation("Created topics: {Topics}", string.Join(", ", topicsToCreate.Select(t => t.Name)));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Topic creation failed (topics may already exist)");
            }
        }

        /// <summary>
        /// Serializer options for JSON deserialization allowing for case-insensitive property matching.
        /// </summary>
        private static readonly JsonSerializerOptions _serializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(allowIntegerValues: true) }
        };

        /// <summary>
        /// Processes a consumed Kafka event by routing it to the appropriate MediatR command based on the event type.
        /// </summary>
        /// <param name="eventType"></param>
        /// <param name="rawJson"></param>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        private async Task ProcessEventAsync(string? eventType, string rawJson, CancellationToken stoppingToken)
        {
            //Create a new scope for MediatR and the DbContext
            using var scope = _scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            //Route the event to the appropriate command handler based on the event type
            switch (eventType)
            {
                case "PaymentConfirmed":
                    var paymentConfirmedEvent = JsonSerializer.Deserialize<EventEnvelope<PaymentConfirmedPayload>>(rawJson, _serializerOptions);
                    if (paymentConfirmedEvent?.Payload != null)
                        await mediator.Send(new ProcessPaymentConfirmedCommand(paymentConfirmedEvent.Payload), stoppingToken);
                    else
                        _logger.LogWarning("Received PaymentConfirmed event with null payload. Raw message: {RawJson}", rawJson);
                    break;

                case "PaymentFailed":
                    var paymentFailedEvent = JsonSerializer.Deserialize<EventEnvelope<PaymentFailedPayload>>(rawJson, _serializerOptions);
                    if (paymentFailedEvent?.Payload != null)
                        await mediator.Send(new ProcessPaymentFailedCommand(paymentFailedEvent.Payload), stoppingToken);
                    else
                        _logger.LogWarning("Received PaymentFailed event with null payload. Raw message: {RawJson}", rawJson);
                    break;

                case "SubscriptionCancelled":
                    var subscriptionCancelledEvent = JsonSerializer.Deserialize<EventEnvelope<SubscriptionCancelledPayload>>(rawJson, _serializerOptions);
                    if (subscriptionCancelledEvent?.Payload != null)
                        await mediator.Send(new ProcessSubscriptionCancelledCommand(subscriptionCancelledEvent.Payload), stoppingToken);
                    else
                        _logger.LogWarning("Received SubscriptionCancelled event with null payload. Raw message: {RawJson}", rawJson);
                    break;
                
                case "SubscriptionRenewed":
                    var subscriptionRenewedEvent = JsonSerializer.Deserialize<EventEnvelope<SubscriptionRenewedPayload>>(rawJson, _serializerOptions);
                    if (subscriptionRenewedEvent?.Payload != null)
                        await mediator.Send(new ProcessSubscriptionRenewedCommand(subscriptionRenewedEvent.Payload), stoppingToken);
                    else
                        _logger.LogWarning("Received SubscriptionRenewed event with null payload. Raw message: {RawJson}", rawJson);
                    break;
                
                case "SubscriptionUpdated":
                    var subscriptionUpdatedEvent = JsonSerializer.Deserialize<EventEnvelope<SubscriptionUpdatedPayload>>(rawJson, _serializerOptions);
                    if (subscriptionUpdatedEvent?.Payload != null)
                        await mediator.Send(new ProcessSubscriptionUpdatedCommand(subscriptionUpdatedEvent.Payload), stoppingToken);
                    else
                        _logger.LogWarning("Received SubscriptionUpdated event with null payload. Raw message: {RawJson}", rawJson);
                    break;
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Routes a failed message to the Dead Letter Queue by publishing it a specified DLQ Kafka topic.
        /// </summary>
        /// <param name="messageJson">The original message JSON that failed to process, which will be sent to the DLQ for later analysis.</param>
        /// <param name="ex">The exception that was thrown during processing.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the publish operation if needed.</param>
        /// <returns></returns>
        private async Task RouteToDeadLetterQueueAsync(string messageJson, Exception ex, CancellationToken cancellationToken)
        {
            var deadLetterQueueMessage = new Message<Null, string>
            {
                Value = messageJson,
                Headers = new Headers
                {
                    { "ExceptionMessage", Encoding.UTF8.GetBytes(ex.Message) },
                    { "StackTrace", Encoding.UTF8.GetBytes(ex.StackTrace ?? string.Empty) },
                    { "FailedAt", Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O")) }
                }
            };

            await _dlqProducer.ProduceAsync(_settings.DeadLetterQueueTopic, deadLetterQueueMessage, cancellationToken);
        }
    }
}