namespace SupportHub.OrganizationAccesss.Infrastructure.Messaging
{
    /// <summary>
    /// Configuration settings for the Kafka producer and consumer.
    /// Includes bootstrap servers, group ID, and topics to publish/subscribe to.
    /// </summary>
    public class KafkaSettings
    {
        //Comma-separated list of Kafka broker addresses, e.g. "broker1:9092,broker2:9092"
        public string BootstrapServers { get; set; } = string.Empty;

        //Unique identifier for the consumer group, e.g. "organization-access-consumers"
        public string GroupId { get; set; } = string.Empty;
        
        //Topic to which events will be published, e.g. "organization-access-events"
        public string PublishTopic { get; set; } = "organization-access-events";
        
        //Topic for messages that failed to process successfully, e.g. "organization-access-dlq"
        public string DeadLetterQueueTopic { get; set; } = "organization-access-dlq";
        
        //Comma-separated list of topics to subscribe to, e.g. "organization-updates, organization-creations"
        public string[] SubscribeTopics { get; set; } = [];
    }
}