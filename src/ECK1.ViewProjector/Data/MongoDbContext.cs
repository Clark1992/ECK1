using MongoDB.Driver;
using ECK1.ViewProjector.Views;

namespace ECK1.ViewProjector.Data
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;
        public MongoDbContext(string connectionString, string databaseName)
        {
            var client = new MongoClient(connectionString);
            _database = client.GetDatabase(databaseName);
        }

        public IMongoCollection<SampleView> Samples => _database.GetCollection<SampleView>("samples");
    }
}
