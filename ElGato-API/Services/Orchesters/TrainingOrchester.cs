using ElGato_API.Data;
using ElGato_API.Interfaces;
using ElGato_API.Interfaces.Orchesters;
using ElGato_API.Models.Training;
using ElGato_API.ModelsMongo.History;
using ElGato_API.ModelsMongo.Training;
using ElGato_API.VM.Training;
using ElGato_API.VM.UserData;
using ElGato_API.VMO.ErrorResponse;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;

namespace ElGato_API.Services.Orchesters
{
    public class TrainingOrchester : ITrainingOrchester
    {
        private readonly IMongoCollection<DailyTrainingDocument> _trainingCollection;
        private readonly IHelperService _helperService;
        private readonly IUserService _userService;
        private readonly ITrainingService _trainingService;
        private readonly ILogger<TrainingOrchester> _logger;
        public TrainingOrchester(IMongoDatabase database, AppDbContext context, ILogger<TrainingOrchester> logger, IHelperService helperService, IUserService userService, ITrainingService trainingService)
        {
            _trainingCollection = database.GetCollection<DailyTrainingDocument>("DailyTraining");
            _helperService = helperService;
            _userService = userService;
            _logger = logger;
            _trainingService = trainingService;
        }

        public async Task<BasicErrorResponse> AddSeriesToAnExercise(string userId, List<AddSeriesToAnExerciseVM> model)
        {
            try
            {
                if (model.IsNullOrEmpty())
                {
                    return new BasicErrorResponse() { ErrorCode = ErrorCodes.ModelStateNotValid, ErrorMessage = $"Invalid model. Check {nameof(AddSeriesToAnExerciseVM)}", Success = false };
                }

                double totalWeight = model.SelectMany(item => item.Series).Sum(s => s.WeightKg * s.Repetitions);
                var statisticsToAdd = new List<UserStatisticsVM>() { new UserStatisticsVM() { Date = model.FirstOrDefault().Date, Type = ModelsMongo.Statistics.StatisticType.WeightLifted, Value = totalWeight } };

                var client = _trainingCollection.Database.Client;

                using (var session = await client.StartSessionAsync())
                {
                    session.StartTransaction();

                    var statsResult = await _userService.AddToUserStatistics(userId, statisticsToAdd, session);
                    if (!statsResult.Success)
                    {
                        await session.AbortTransactionAsync();
                        return statsResult;
                    }

                    var writeTasks = model.Select(m => _trainingService.WriteSeriesForAnExercise(userId, m, session));
                    var updateTasks = model.Select(m => _trainingService.UpdateExerciseHistory(userId, m.HistoryUpdate, m.Date, session));

                    var allTasks = writeTasks.Concat(updateTasks);
                    var results = await Task.WhenAll(allTasks);

                    var failed = results.Where(r => !r.Success).FirstOrDefault();
                    if (failed != null)
                    {
                        await session.AbortTransactionAsync();
                        return failed ?? new BasicErrorResponse() { ErrorMessage = "Update failed.", ErrorCode = ErrorCodes.Failed, Success = false };
                    }

                    await session.CommitTransactionAsync();
                    return new BasicErrorResponse { Success = true, ErrorCode = ErrorCodes.None, ErrorMessage = "Sucess" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to add series to an exercise. UserId: {userId} Method: {nameof(AddSeriesToAnExercise)}");
                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, Success = false, ErrorMessage = $"An error occured: {ex.Message}" });
            }
        }

        public async Task<BasicErrorResponse> UpdateExerciseSeries(string userId, List<UpdateExerciseSeriesVM> model)
        {
            try
            {
                var client = _trainingCollection.Database.Client;

                using (var session = await client.StartSessionAsync())
                {
                    session.StartTransaction();

                    double updateWeight = model.Select(vm => vm.HistoryUpdate.ExerciseData.Series).SelectMany(seriesList => seriesList)
                                                  .Sum(series => series.WeightKg * series.Repetitions);

                    var wholeDayExercisesPublicIds = await _trainingService.GetExerciseInTrainingDayPublicIds(userId, model.FirstOrDefault().Date, session);
                    var exercisesPublicId = model.Select(a=>a.ExercisePublicId).ToList();

                    wholeDayExercisesPublicIds.RemoveAll(x => exercisesPublicId.Contains(x));

                    double rest = await _trainingService.GetTotalExerciseWeightValue(userId, model.FirstOrDefault().Date, wholeDayExercisesPublicIds, session);


                    var statisticsToAdd = new List<UserStatisticsVM>() { new UserStatisticsVM() { Date = model.FirstOrDefault().Date, Type = ModelsMongo.Statistics.StatisticType.WeightLifted, Value = (rest + updateWeight) } };

                    var statsResult = await _userService.AddToUserStatistics(userId, statisticsToAdd, session);
                    if (!statsResult.Success)
                    {
                        await session.AbortTransactionAsync();
                        return statsResult;
                    }

                    var patchTasks = model.Select(m => _trainingService.UpdateExerciseSeries(userId, m, session));
                    var patchHistory = model.Select(m => _trainingService.UpdateExerciseHistory(userId, m.HistoryUpdate, m.Date, session));

                    var allTasks = patchTasks.Concat(patchHistory);
                    var res = await Task.WhenAll(allTasks);

                    var failed = res.Where(r => !r.Success).ToList();
                    if (failed.Any())
                    {
                        var firstError = failed.First();
                        _logger.LogWarning($"Update failed. Method: {nameof(UpdateExerciseSeries)}");
                        return firstError ?? new BasicErrorResponse() { ErrorCode = ErrorCodes.Failed, Success = false, ErrorMessage = "Update failed." };
                    }

                    await session.CommitTransactionAsync();
                    return new BasicErrorResponse { Success = true, ErrorCode = ErrorCodes.None, ErrorMessage = "Sucess" };
                }
              
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to update exercise series. UserId: {userId} Method: {nameof(UpdateExerciseSeries)}");
                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, Success = false, ErrorMessage = $"An error occured: {ex.Message}" });
            }
        }

        public async Task<BasicErrorResponse> RemoveSeriesFromAnExercise(string userId, List<RemoveSeriesFromExerciseVM> model)
        {
            try
            {
                var client = _trainingCollection.Database.Client;

                using (var session = await client.StartSessionAsync())
                {
                    session.StartTransaction();

                    double updateWeight = model.Select(vm => vm.HistoryUpdate.ExerciseData.Series).SelectMany(seriesList => seriesList)
                                                  .Sum(series => series.WeightKg * series.Repetitions);

                    var wholeDayExercisesPublicIds = await _trainingService.GetExerciseInTrainingDayPublicIds(userId, model.FirstOrDefault().Date, session);
                    var exercisesPublicId = model.Select(a => a.ExercisePublicId).ToList();

                    wholeDayExercisesPublicIds.RemoveAll(x => exercisesPublicId.Contains(x));

                    double rest = await _trainingService.GetTotalExerciseWeightValue(userId, model.FirstOrDefault().Date, wholeDayExercisesPublicIds, session);

                    var statisticsToAdd = new List<UserStatisticsVM>() { new UserStatisticsVM() { Date = model.FirstOrDefault().Date, Type = ModelsMongo.Statistics.StatisticType.WeightLifted, Value = (rest + updateWeight) } };

                    var statsResult = await _userService.AddToUserStatistics(userId, statisticsToAdd, session);
                    if (!statsResult.Success)
                    {
                        await session.AbortTransactionAsync();
                        return statsResult;
                    }

                    var deleteTasks = model.Select(m => _trainingService.RemoveSeriesFromAnExercise(userId, m, session));
                    var patchTasks = model.Select(m => _trainingService.UpdateExerciseHistory(userId, m.HistoryUpdate, m.Date, session));

                    var allTasks = deleteTasks.Concat(patchTasks);
                    var res = await Task.WhenAll(allTasks);

                    var failed = res.Where(r => !r.Success).ToList();

                    if (failed.Any())
                    {
                        var firstError = failed.First();
                        _logger.LogWarning($"Update failed. Method: {nameof(RemoveSeriesFromAnExercise)}");
                        return firstError ?? new BasicErrorResponse() { ErrorCode = ErrorCodes.Failed, Success = false, ErrorMessage = "Update failed." };
                    }

                    await session.CommitTransactionAsync();
                    return new BasicErrorResponse { Success = true, ErrorCode = ErrorCodes.None, ErrorMessage = "Sucess" };
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to remove exercise series. UserId: {userId} Method: {nameof(RemoveSeriesFromAnExercise)}");
                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, Success = false, ErrorMessage = $"An error occured: {ex.Message}" });
            }
        }

        public async Task<BasicErrorResponse> RemoveExercisesFromTrainingDay(string userId, List<RemoveExerciseFromTrainingDayVM> model)
        {
            try
            {
                var client = _trainingCollection.Database.Client;

                using (var session = await client.StartSessionAsync())
                {
                    session.StartTransaction();

                    var wholeDayExercisesPublicIds = await _trainingService.GetExerciseInTrainingDayPublicIds(userId, model.FirstOrDefault().Date, session);
                    var exercisesPublicId = model.Select(a => a.ExerciseId).ToList();

                    wholeDayExercisesPublicIds.RemoveAll(x => exercisesPublicId.Contains(x));

                    double newWeightUpd = await _trainingService.GetTotalExerciseWeightValue(userId, model.FirstOrDefault().Date, wholeDayExercisesPublicIds, session);

                    var statisticsToAdd = new List<UserStatisticsVM>() { new UserStatisticsVM() { Date = model.FirstOrDefault().Date, Type = ModelsMongo.Statistics.StatisticType.WeightLifted, Value = newWeightUpd } };

                    var statsResult = await _userService.AddToUserStatistics(userId, statisticsToAdd, session);
                    if (!statsResult.Success)
                    {
                        await session.AbortTransactionAsync();
                        return statsResult;
                    }

                    var removeTasks = model.Select(m => _trainingService.RemoveExerciseFromTrainingDay(userId, m, session));
                    var res = await Task.WhenAll(removeTasks);

                    var failed = res.Where(r => !r.Success).ToList();
                    if (failed.Any())
                    {
                        var firstError = failed.First();
                        return firstError ?? new BasicErrorResponse() { ErrorCode = ErrorCodes.Failed, Success = false, ErrorMessage = "Update failed." };
                    }

                    await session.CommitTransactionAsync();
                    return new BasicErrorResponse { Success = true, ErrorCode = ErrorCodes.None, ErrorMessage = "Sucess" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to remove exercise. UserId: {userId} Method: {nameof(RemoveExercisesFromTrainingDay)}");
                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, Success = false, ErrorMessage = $"An error occured: {ex.Message}" });
            }
        }
    }
}
