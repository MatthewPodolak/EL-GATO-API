using ElGato_API.Data;
using ElGato_API.Interfaces;
using ElGato_API.Interfaces.Orchesters;
using ElGato_API.ModelsMongo.Statistics;
using ElGato_API.VM.UserData;
using ElGato_API.VMO.Achievments;
using ElGato_API.VMO.ErrorResponse;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace ElGato_API.Services.Orchesters
{
    public class UserDataOrchester : IUserDataOrchester
    {
        private readonly AppDbContext _context;
        private readonly IMongoCollection<UserStatisticsDocument> _userStatisticsDocument;
        private readonly IUserService _userService;
        private readonly IAchievmentService _achievmentService;
        private readonly ILogger<UserDataOrchester> _logger;
        public UserDataOrchester(AppDbContext context, IMongoDatabase database, IUserService service, IAchievmentService achievmentService, ILogger<UserDataOrchester> logger)
        {
            _context = context;
            _userStatisticsDocument = database.GetCollection<UserStatisticsDocument>("Statistics");
            _userService = service;
            _achievmentService = achievmentService;
            _logger = logger;
        }

        public async Task<AchievmentResponse> AddStepsForUser(string userId, AddStepsVM model)
        {
            try
            {
                var client = _userStatisticsDocument.Database.Client;

                using (var session = await client.StartSessionAsync())
                {
                    session.StartTransaction();

                    var statisticsModel = new List<UserStatisticsVM>()
                    {
                        new UserStatisticsVM()
                        {
                            Type = StatisticType.StepsTaken,
                            Date = model.Date,
                            Value = model.Steps
                        }
                    };

                    var saveUserStatisticsTask = await _userService.AddToUserStatistics(userId, statisticsModel, session, true);
                    if (!saveUserStatisticsTask.Success)
                    {
                        await session.AbortTransactionAsync();
                        _logger.LogWarning($"Failed while trying to add steps to the statistics. UserId: {userId} Method: {nameof(AddStepsForUser)}");
                        return new AchievmentResponse() { Status = saveUserStatisticsTask };
                    }

                    await using var sqlTx = await _context.Database.BeginTransactionAsync();

                    var familyResult = await _achievmentService.GetCurrentAchivmentIdFromFamily("STEPS", userId, _context);
                    if (!familyResult.error.Success)
                    {
                        await sqlTx.RollbackAsync();
                        await session.AbortTransactionAsync();
                        _logger.LogError($"Failed while trying to retrive current achievment table. UserId: {userId} Method: {nameof(AddStepsForUser)}");
                        return new AchievmentResponse() { Status = familyResult.error };
                    }

                    if (string.IsNullOrEmpty(familyResult.achievmentName))
                    {
                        await session.CommitTransactionAsync();
                        return new AchievmentResponse { Status = ErrorResponse.Ok() };
                    }

                    var incrementTask = await _achievmentService.IncrementAchievmentProgress(familyResult.achievmentName, userId, model.Steps, _context);
                    if (!incrementTask.error.Success)
                    {
                        await sqlTx.RollbackAsync();
                        await session.AbortTransactionAsync();
                        return new AchievmentResponse() { Status = incrementTask.error };
                    }
                    
                    if(incrementTask.ach != null)
                    {
                        await sqlTx.CommitAsync();
                        await session.CommitTransactionAsync();

                        return incrementTask.ach;
                    }


                    await sqlTx.CommitAsync();
                    await session.CommitTransactionAsync();
                    return new AchievmentResponse() { Status = ErrorResponse.Ok() };
                }
            }
            catch (Exception ex)
            {
                return new AchievmentResponse() { Status = ErrorResponse.Internal(ex.Message) };
            }
        }
    }
}
