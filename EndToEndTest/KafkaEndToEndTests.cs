using Xunit;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ConsumerService.Data;
using ConsumerService.Models;
using ProducerService.Models;
using ProducerService.Services;
using ConsumerService.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;

namespace EndToEndTest
{
    public class KafkaEndToEndTests
    {
        private const string ConnectionString = "Server=localhost;Database=PostDb;Trusted_Connection=True;TrustServerCertificate=True;";
        private readonly IKafkaProducer _kafkaProducer;
        private readonly IKafkaConsumer _kafkaConsumer;

        public KafkaEndToEndTests()
        {
            // Setup DI for IKafkaProducer and IKafkaConsumer
            var services = new ServiceCollection();
            services.AddSingleton<IKafkaProducer, KafkaProducer>(); 
            services.AddSingleton<IKafkaConsumer, KafkaConsumer>();

            var provider = services.BuildServiceProvider();
            _kafkaProducer = provider.GetRequiredService<IKafkaProducer>();
            _kafkaConsumer = provider.GetRequiredService<IKafkaConsumer>();
        }

        [Fact]
        public async Task KafkaProducer_Publishes_Post_And_Consumer_Writes_To_Db()
        {
            try
            {
                // Arrange
                var post = new ProducerService.Models.Post
                {
                    id = 777,
                    userId = 999,
                    title = "Reused KafkaProducer",
                    body = "Post from real KafkaProducer"
                };

                await ClearDatabaseAsync();

                // Act
                await _kafkaProducer.ProduceAsync(post); 
                var cts = new CancellationTokenSource();
                var consumeTask = _kafkaConsumer.StartConsumingAsync(cts.Token);

                // Let consumer process the message
                await Task.Delay(5000);

                // Cancel the consumer task after processing
                cts.Cancel();
                await consumeTask; // Ensure consumer task is completed

                // Assert
                ConsumerService.Models.Post saved = await GetPostByIdAsync(post.id);
                Assert.NotNull(saved);
                Assert.Equal(post.title, saved.title);
            }
            catch (Exception ex)
            {
                // Log exception if necessary
                Assert.True(false, ex.Message);
            }
        }

        private async Task ClearDatabaseAsync()
        {
            using var db = GetDbContext();
            db.Posts.RemoveRange(db.Posts);
            await db.SaveChangesAsync();
        }

        private async Task<ConsumerService.Models.Post?> GetPostByIdAsync(int id)
        {
            using var db = GetDbContext();
            return await db.Posts.FirstOrDefaultAsync(p => p.id == id);
        }

        private PostDbContext GetDbContext()
        {
            return new PostDbContext(new DbContextOptionsBuilder<PostDbContext>()
                .UseSqlServer(ConnectionString)
                .Options);
        }
    }
}