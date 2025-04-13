using Xunit;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ConsumerService.Data;
using ConsumerService.Models;
using System.Linq;
using System;
using System.Threading;
using Confluent.Kafka;


namespace EndToEndTest
{
    public class KafkaEndToEndTests
    {
        private readonly string _connectionString = "Server=localhost;Database=PostDb;Trusted_Connection=True;TrustServerCertificate=True;";

        [Fact]
        public async Task Producer_To_Kafka_To_Consumer_Writes_To_Database()
        {
            // Arrange
            using var db = new PostDbContext(new DbContextOptionsBuilder<PostDbContext>()
                .UseSqlServer(_connectionString)
                .Options);

            // Clear DB to start fresh
            db.Posts.RemoveRange(db.Posts);
            await db.SaveChangesAsync();

            // Act
            var testPost = new Post
            {
                id = 99,
                userId = 10,
                title = "E2E Test Title",
                body = "This is an E2E body"
            };

            var kafkaConfig = new Confluent.Kafka.ProducerConfig { BootstrapServers = "localhost:9092" };

            using var producer = new Confluent.Kafka.ProducerBuilder<Null, string>(kafkaConfig).Build();
            var json = System.Text.Json.JsonSerializer.Serialize(testPost);

            await producer.ProduceAsync("sample-topic", new Confluent.Kafka.Message<Null, string> { Value = json });

            // Flush to make sure it’s sent
            producer.Flush(TimeSpan.FromSeconds(2));

            // Wait for ConsumerService to consume and save
            await Task.Delay(5000); 

            // Assert
            var saved = db.Posts.FirstOrDefault(p => p.id == 99);
            Assert.NotNull(saved);
            Assert.Equal("E2E Test Title", saved.title);
        }
    }
}