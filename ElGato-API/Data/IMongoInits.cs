using MongoDB.Driver;

namespace ElGato_API.Data
{
    public interface IMongoInits
    {
        Task CreateUserDietDocument(string userId, IClientSessionHandle session = null, CancellationToken ct = default);
        Task CreateUserTrainingDocument(string userId, IClientSessionHandle session = null, CancellationToken ct = default);
        Task CreateUserExerciseHistoryDocument(string userId, IClientSessionHandle session = null, CancellationToken ct = default);
    }
}