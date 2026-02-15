using MongoDB.Driver;
using ECK1.CommonUtils.OpenTelemetry;
using ECK1.ViewProjector.Views;

namespace ECK1.ViewProjector.Data
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;
        public MongoDbContext(string connectionString, string databaseName)
        {
            var settings = MongoClientSettings
                .FromConnectionString(connectionString)
                .AddOpenTelemetryInstrumentation();
            var client = new MongoClient(settings);
            _database = client.GetDatabase(databaseName);
        }

        public IMongoCollection<SampleView> Samples => _database.GetCollection<SampleView>("samples");
        public IMongoCollection<Sample2View> Sample2s => _database.GetCollection<Sample2View>("sample2s");
    }
}
