using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace EndToEndTest
{
    public class EndToEndTests
    {
        [Fact]
        public async Task PostData_IsConsumed_AndStoredInDb()
        {
            // Act: Call producer (assume it hits a local endpoint or pushes via Kafka)
            using var httpClient = new HttpClient();
            var response = await httpClient.PostAsync("http://localhost:5000/api/posts", null);
            response.EnsureSuccessStatusCode();

            // Wait for Kafka + Consumer to do their work
            await Task.Delay(8000);

            // Assert: Data is in SQL Server
            using var connection = new SqlConnection("Server=localhost,1433;Database=PostDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True;");
            await connection.OpenAsync();

            var cmd = new SqlCommand("SELECT COUNT(*) FROM Posts", connection);
            var count = (int)await cmd.ExecuteScalarAsync();

            Assert.True(count > 0, "No posts found in database");
        }
    }
}
