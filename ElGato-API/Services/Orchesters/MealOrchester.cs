using ElGato_API.Data;
using ElGato_API.Interfaces;
using ElGato_API.Interfaces.Orchesters;
using ElGato_API.VM.Meal;
using ElGato_API.VMO.Achievments;
using ElGato_API.VMO.ErrorResponse;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace ElGato_API.Services.Orchesters
{
    public class MealOrchester : IMealOrchester
    {
        private readonly ILogger<MealOrchester> _logger;
        private readonly IMongoClient _mongoClient;
        private readonly AppDbContext _context;
        private readonly IAchievmentService _achievmentService;
        private readonly IMealService _mealService;
        public MealOrchester(ILogger<MealOrchester> logger, IMongoClient mongoClient, AppDbContext context, IAchievmentService achievmentService, IMealService mealService)
        {
            _logger = logger;
            _mongoClient = mongoClient;
            _context = context;
            _achievmentService = achievmentService;
            _mealService = mealService;
        }

        public async Task<AchievmentResponse> ProcessAndPublishMeal(string userId, PublishMealVM model)
        {
            try
            {
                using var mongoSession = await _mongoClient.StartSessionAsync();
                mongoSession.StartTransaction();

                await using var sqlTx = await _context.Database.BeginTransactionAsync();

                var publishRes = await _mealService.PublishMeal(userId, model, mongoSession);
                if (!publishRes.Success)
                {
                    await sqlTx.RollbackAsync();
                    await mongoSession.AbortTransactionAsync();

                    return new AchievmentResponse() { Status = publishRes };
                }

                var currentAchievmentCounter = await _achievmentService.GetCurrentAchivmentIdFromFamily("COOK", userId, _context);
                if (!currentAchievmentCounter.error.Success)
                {
                    await sqlTx.RollbackAsync();
                    await mongoSession.AbortTransactionAsync();

                    return new AchievmentResponse() { Status = ErrorResponse.Failed() };
                }

                if (!string.IsNullOrEmpty(currentAchievmentCounter.achievmentName))
                {
                    var achievmentRes = await _achievmentService.IncrementAchievmentProgress(currentAchievmentCounter.achievmentName, userId, 1, _context);
                    if (!achievmentRes.error.Success)
                    {
                        await sqlTx.RollbackAsync();
                        await mongoSession.AbortTransactionAsync();

                        return new AchievmentResponse() { Status = ErrorResponse.Failed() };
                    }

                    await sqlTx.CommitAsync();
                    await mongoSession.CommitTransactionAsync();
                    return achievmentRes.ach ?? new AchievmentResponse() { Status = ErrorResponse.Ok() };
                }

                await sqlTx.CommitAsync();
                await mongoSession.CommitTransactionAsync();
                return new AchievmentResponse() { Status = ErrorResponse.Ok() };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Publishing new meal orchester failed. Method: {nameof(ProcessAndPublishMeal)} Model: {model}");
                return new AchievmentResponse() { Status = ErrorResponse.Internal(ex.Message) };
            }
        }
    }
}