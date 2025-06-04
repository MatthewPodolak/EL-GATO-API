using ElGato_API.Data;
using ElGato_API.Interfaces;
using ElGato_API.Interfaces.Orchesters;
using ElGato_API.ModelsMongo.Cardio;
using ElGato_API.ModelsMongo.History;
using ElGato_API.ModelsMongo.Statistics;
using ElGato_API.VM.Cardio;
using ElGato_API.VM.UserData;
using ElGato_API.VMO.Achievments;
using ElGato_API.VMO.ErrorResponse;
using MongoDB.Driver;

namespace ElGato_API.Services.Orchesters
{
    public class CardioOrchester : ICardioOrchester
    {
        private readonly ILogger<CardioOrchester> _logger;
        private readonly AppDbContext _context;
        private readonly IHelperService _helperService;
        private readonly ICardioService _cardioService;
        private readonly IUserService _userService;
        private readonly IAchievmentService _achievmentService;
        private readonly IMongoCollection<DailyCardioDocument> _cardioDocument;
        public CardioOrchester(AppDbContext context, ICardioService cardioService, ILogger<CardioOrchester> logger, IMongoDatabase database, IHelperService helperService, IUserService userService, IAchievmentService achievmentService)
        {
            _context = context;
            _logger = logger;
            _helperService = helperService;
            _cardioService = cardioService;
            _userService = userService;
            _achievmentService = achievmentService;
            _cardioDocument = database.GetCollection<DailyCardioDocument>("DailyCardio");
        }

        public async Task<AchievmentResponse> AddExerciseToTrainingDay(string userId, AddCardioExerciseVM model)
        {
            try
            {
                var client = _cardioDocument.Database.Client;

                using (var session = await client.StartSessionAsync())
                {
                    session.StartTransaction();

                    var saveExerciseTask = await _cardioService.AddExerciseToTrainingDay(userId, model, session);
                    if (!saveExerciseTask.Success)
                    {
                        await session.AbortTransactionAsync();
                        _logger.LogWarning($"Failed while trying to add new exercise to cardio training day. Method: {nameof(AddExerciseToTrainingDay)}");
                        return new AchievmentResponse() { Status = saveExerciseTask };
                    }

                    var statisticsModel = new List<UserStatisticsVM>() 
                    {
                        new UserStatisticsVM(){ Type = StatisticType.CaloriesBurnt, Value = model.CaloriesBurnt, Date = model.Date },
                        new UserStatisticsVM(){ Type = StatisticType.TotalDistance, Value = model.Distance, Date = model.Date },
                        new UserStatisticsVM(){ Type = StatisticType.ActvSessionsCount, Value = 1, Date = model.Date },
                        new UserStatisticsVM(){ Type = StatisticType.TimeSpend, TimeValue = model.Duration, Date = model.Date },
                    };

                    var saveUserStatisticsTask = await _userService.AddToUserStatistics(userId, statisticsModel, session, true);
                    if (!saveUserStatisticsTask.Success)
                    {
                        await session.AbortTransactionAsync();
                        _logger.LogWarning($"Failed while trying to add new exercise to cardio training day. Method: {nameof(AddExerciseToTrainingDay)}");
                        return new AchievmentResponse() { Status = saveUserStatisticsTask };
                    }

                    await using var sqlTx = await _context.Database.BeginTransactionAsync();
                    var badgeTask = await _achievmentService.CheckAndAddBadgeProgressForUser(userId, new VM.Achievments.BadgeIncDataVM
                    {
                        ActivityType = model.ActivityType,
                        CaloriesBurnt = model.CaloriesBurnt,
                        Distance = model.Distance,
                    }, _context);

                    var familyResultCardio = await _achievmentService.GetCurrentAchivmentIdFromFamily("CARDIO", userId, _context);
                    var familyResultCalorie = await _achievmentService.GetCurrentAchivmentIdFromFamily("CALORIE", userId, _context);

                    var (badgeIncrement, achievmentFamilyResult, achievmentFamilyResultCalorie) = (badgeTask, familyResultCardio, familyResultCalorie);

                    if(!badgeIncrement.Success || !achievmentFamilyResult.error.Success || !achievmentFamilyResultCalorie.error.Success)
                    {
                        await sqlTx.RollbackAsync();
                        await session.AbortTransactionAsync();
                        _logger.LogError($"Failed while trying to get achievment data. Method {nameof(AddExerciseToTrainingDay)}");
                        return new AchievmentResponse() { Status = new BasicErrorResponse() { ErrorCode = ErrorCodes.Failed, Success = false, ErrorMessage = "Failed while trying to get achievment progress data." } };
                    }

                    Task<(BasicErrorResponse error, AchievmentResponse? ach)> incrementCardioTask =
                        Task.FromResult((
                            error: new BasicErrorResponse { Success = true, ErrorCode = ErrorCodes.None },
                            ach: (AchievmentResponse?)null
                        ));

                    Task<(BasicErrorResponse error, AchievmentResponse? ach)> incrementCalorieTask =
                        Task.FromResult((
                            error: new BasicErrorResponse { Success = true, ErrorCode = ErrorCodes.None },
                            ach: (AchievmentResponse?)null
                        ));


                    if (!string.IsNullOrEmpty(achievmentFamilyResult.achievmentName))
                    {
                        incrementCardioTask = _achievmentService.IncrementAchievmentProgress(achievmentFamilyResult.achievmentName, userId, 1, _context);                                     
                    }

                    if (!string.IsNullOrEmpty(achievmentFamilyResultCalorie.achievmentName))
                    {
                        incrementCalorieTask = _achievmentService.IncrementAchievmentProgress(achievmentFamilyResultCalorie.achievmentName, userId, model.CaloriesBurnt, _context);
                    }

                    await Task.WhenAll(incrementCardioTask, incrementCalorieTask);
                    var (cardioAchRes, calorieAchRes) = (incrementCardioTask.Result, incrementCalorieTask.Result);
                    if (cardioAchRes.error.Success && cardioAchRes.ach.Achievment != null)
                    {
                        await sqlTx.CommitAsync();
                        await session.CommitTransactionAsync();
                        return cardioAchRes.ach;
                    }

                    if (calorieAchRes.error.Success && calorieAchRes.ach.Achievment != null)
                    {
                        await sqlTx.CommitAsync();
                        await session.CommitTransactionAsync();
                        return calorieAchRes.ach;
                    }

                    await sqlTx.CommitAsync();
                    await session.CommitTransactionAsync();

                    return new AchievmentResponse() { Status = new BasicErrorResponse() { ErrorCode = ErrorCodes.None, Success = true, ErrorMessage = "Sucess" } };
                }
               
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to add exercises to cardio training day. UserId: {userId} Method: {nameof(AddExerciseToTrainingDay)}");
                return new AchievmentResponse() { Status = new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, Success = false, ErrorMessage = $"An error occured: {ex.Message}" } };
            }
        }

        public async Task<BasicErrorResponse> DeleteExercisesFromCardioTrainingDay(string userId, DeleteExercisesFromCardioTrainingVM model)
        {
            try
            {
                var client = _cardioDocument.Database.Client;

                using (var session = await client.StartSessionAsync())
                {
                    session.StartTransaction();

                    var statisticsToRemove = await _cardioService.GetStatisticsDataFromExercise(userId, model.ExercisesIdToRemove, model.Date, session);
                    if (!statisticsToRemove.error.Success)
                    {
                        await session.AbortTransactionAsync();
                        _logger.LogWarning($"Failed while trying to get exercises statistics from cardio training day. UserId: {userId} Method: {nameof(DeleteExercisesFromCardioTrainingDay)}");
                        return statisticsToRemove.error;
                    }


                    var invertedStats = statisticsToRemove.data
                        .Select(vm => new UserStatisticsVM
                        {
                            Type = vm.Type,
                            Date = vm.Date,
                            Value = (vm.Type == StatisticType.TimeSpend) ? 0 : -vm.Value,
                            TimeValue = (vm.Type == StatisticType.TimeSpend && vm.TimeValue.HasValue) ? vm.TimeValue.Value.Negate() : TimeSpan.Zero
                        })
                        .ToList();

                    var saveUserStatisticsTask = await _userService.AddToUserStatistics(userId, invertedStats, session, true);
                    if (!saveUserStatisticsTask.Success)
                    {
                        await session.AbortTransactionAsync();
                        return saveUserStatisticsTask;
                    }


                    var exerciseDeletion = await _cardioService.DeleteExercisesFromCardioTrainingDay(userId, model, session);
                    if (!exerciseDeletion.Success)
                    {
                        await session.AbortTransactionAsync();
                        _logger.LogWarning($"Failed while trying to delete exercises from cardio training day. UserId: {userId} Method: {nameof(DeleteExercisesFromCardioTrainingDay)}");
                        return exerciseDeletion;
                    }

                    await session.CommitTransactionAsync();
                }

                return new BasicErrorResponse() { ErrorCode = ErrorCodes.None, Success = true, ErrorMessage = "Sucess" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to delete exercises from cardio training day. UserId: {userId} Method: {nameof(DeleteExercisesFromCardioTrainingDay)}");
                return new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, Success = false, ErrorMessage = $"An error occured: {ex.Message}" };
            }
        }
    }
}
