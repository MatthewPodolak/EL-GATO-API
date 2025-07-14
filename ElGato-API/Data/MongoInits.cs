using ElGato_API.Models.User;
using ElGato_API.ModelsMongo.Diet;
using ElGato_API.ModelsMongo.History;
using ElGato_API.ModelsMongo.Training;
using MongoDB.Driver;

namespace ElGato_API.Data
{
    public class MongoInits : IMongoInits
    {
        private readonly IMongoCollection<DietDocument> _dietCollection;
        private readonly IMongoCollection<DailyTrainingDocument> _trainingCollection;
        private readonly IMongoCollection<ExercisesHistoryDocument> _exercisesHistoryCollection;

        public MongoInits(IMongoDatabase database)
        {
            _dietCollection = database.GetCollection<DietDocument>("DailyDiet");
            _trainingCollection = database.GetCollection<DailyTrainingDocument>("DailyTraining");
            _exercisesHistoryCollection = database.GetCollection<ExercisesHistoryDocument>("ExercisesHistory");
        }

        public async Task CreateUserDietDocument(string userId, IClientSessionHandle session, CancellationToken ct = default)
        {
            try
            {
                var existing = await _dietCollection.Find(session, d => d.UserId == userId).FirstOrDefaultAsync(ct);

                if (existing == null)
                {
                    var newDoc = new DietDocument
                    {
                        UserId = userId,
                        DailyPlans = new List<DailyDietPlan>()
                    };

                    await _dietCollection.InsertOneAsync(session, newDoc, cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error creating user diet document", ex);
            }
        }

        public async Task CreateUserExerciseHistoryDocument(string userId, IClientSessionHandle session, CancellationToken ct = default)
        {
            try
            {
                var existing = await _exercisesHistoryCollection.Find(session, h => h.UserId == userId).FirstOrDefaultAsync(ct);

                if (existing == null)
                {
                    var newDoc = new ExercisesHistoryDocument
                    {
                        UserId = userId,
                        ExerciseHistoryLists = new List<ExerciseHistoryList>(),
                    };

                    await _exercisesHistoryCollection.InsertOneAsync(session, newDoc, cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error creating user exercise‐history document", ex);
            }
        }

        public async Task CreateUserTrainingDocument(string userId, IClientSessionHandle session, CancellationToken ct = default)
        {
            try
            {
                var existing = await _trainingCollection.Find(session, t => t.UserId == userId).FirstOrDefaultAsync(ct);

                if (existing == null)
                {
                    var newDoc = new DailyTrainingDocument
                    {
                        UserId = userId,
                        Trainings = new List<DailyTrainingPlan>()
                    };

                    await _trainingCollection.InsertOneAsync(session, newDoc, cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error creating user training document", ex);
            }
        }
    }
}