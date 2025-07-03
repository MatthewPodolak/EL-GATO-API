using ElGato_API.Controllers;
using ElGato_API.Data;
using ElGato_API.Interfaces;
using ElGato_API.Migrations;
using ElGato_API.Models.User;
using ElGato_API.ModelsMongo.Diet;
using ElGato_API.ModelsMongo.History;
using ElGato_API.ModelsMongo.Statistics;
using ElGato_API.VM.UserData;
using ElGato_API.VMO.Community;
using ElGato_API.VMO.ErrorResponse;
using ElGato_API.VMO.User;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace ElGato_API.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<UserService> _logger;
        private readonly IMongoCollection<ExercisesHistoryDocument> _exercisesHistoryCollection;
        private readonly IMongoCollection<DietHistoryDocument> _dietHistoryCollection;
        private readonly IMongoCollection<DietDocument> _dailyDietCollection;
        private readonly IMongoCollection<UserStatisticsDocument> _userStatisticsDocument;
        private readonly IHelperService _helperService;
        public UserService(AppDbContext dbContext, ILogger<UserService> logger, IMongoDatabase database, IHelperService helperService) 
        { 
            _dbContext = dbContext;
            _logger = logger;
            _exercisesHistoryCollection = database.GetCollection<ExercisesHistoryDocument>("ExercisesHistory");
            _dietHistoryCollection = database.GetCollection<DietHistoryDocument>("DietHistory");
            _dailyDietCollection = database.GetCollection<DietDocument>("DailyDiet");
            _userStatisticsDocument = database.GetCollection<UserStatisticsDocument>("Statistics");
            _helperService = helperService;
        }

        public async Task<(ErrorResponse error, string? data)> GetSystem(string userId)
        {
            try
            {
                var res = await _dbContext.AppUser.FirstOrDefaultAsync(a=>a.Id == userId);
                if(res == null)
                {
                    return (ErrorResponse.NotFound($"user with specified id not found"), null);
                }

                return (ErrorResponse.Ok(), res.Metric ? "metric" : "imperial");
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, $"Failed while trying to get system. UserId: {userId} Method: {nameof(GetSystem)}");
                return (ErrorResponse.Internal(ex.Message), null);
            }            
        }

        public async Task<(ErrorResponse error, UserCalorieIntake model)> GetUserCalories(string userId)
        {
            ErrorResponse error = new ErrorResponse() { Success = false };
            UserCalorieIntake userCalorieIntake = new UserCalorieIntake();

            try
            {
                var res = await _dbContext.Users.Include(x=>x.CalorieInformation).FirstOrDefaultAsync(a=>a.Id == userId);
                if (res == null || res.CalorieInformation == null) 
                {
                    error.ErrorMessage = "User calorie intake information not found.";
                    error.ErrorCode = ErrorCodes.NotFound;
                    return (error, userCalorieIntake);
                }

                userCalorieIntake.Kcal = res.CalorieInformation.Kcal;
                userCalorieIntake.Carbs = res.CalorieInformation.Carbs;
                userCalorieIntake.Fats = res.CalorieInformation.Fat;
                userCalorieIntake.Protein = res.CalorieInformation.Protein;

                error.Success = true;
                error.ErrorCode = ErrorCodes.None;
                return (error, userCalorieIntake);
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, $"Failed while trying to get user calories intake. UserId: {userId} Method: {nameof(GetUserCalories)}");
                error.ErrorMessage = ex.Message;
                error.ErrorCode = ErrorCodes.Internal;
                return (error, userCalorieIntake);
            }
        }

        public async Task<(ErrorResponse error, UserCalorieIntake? model)> GetCurrentCalories(string userId, DateTime date)
        {
            try
            {
                var userDietCollection = await _dailyDietCollection.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if (userDietCollection == null)
                {
                    _logger.LogWarning($"user {userId} daily diet collection does not exist. creating.");
                    var newDoc = await _helperService.CreateMissingDoc(userId, _dailyDietCollection);
                    if (newDoc == null)
                    {
                        return (ErrorResponse.NotFound($"User daily diet collection not found."), null);
                    }

                    return (ErrorResponse.Ok(), new UserCalorieIntake());
                }

                var targetDay = userDietCollection.DailyPlans.FirstOrDefault(a => a.Date == date);
                var vmo = new UserCalorieIntake();

                if (targetDay != null)
                {
                    foreach (var meal in targetDay.Meals)
                    {
                        foreach(var ing in meal.Ingridient)
                        {
                            vmo.Protein += ((ing.Proteins * ing.WeightValue) / ing.PrepedFor);
                            vmo.Carbs += ((ing.Carbs * ing.WeightValue) / ing.PrepedFor);
                            vmo.Fats += ((ing.Fats * ing.WeightValue) / ing.PrepedFor);
                            vmo.Kcal += ((ing.EnergyKcal * ing.WeightValue) / ing.PrepedFor);
                        }
                    }
                }

                return (ErrorResponse.Ok(), vmo);
            }
            catch(Exception ex)
            {
                return (ErrorResponse.Internal(ex.Message), null);
            }
        }

        public async Task<(ErrorResponse error, double weight)> GetCurrentUserWeight(string userId)
        {
            try
            {
                var res = await _dbContext.AppUser.Include(a=>a.UserInformation).FirstOrDefaultAsync(a=>a.Id == userId);
                if(res == null || res.UserInformation == null)
                {
                    _logger.LogWarning($"User record with id: {userId} not found while trying to get user current weight. Method: {nameof(GetCurrentUserWeight)}");
                    return (ErrorResponse.NotFound("User with given id and its weight not found."), 0);
                }

                return (ErrorResponse.Ok(), res.UserInformation.Weight ?? 0);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Error occured while trying to get current user weight UserId: {userId} Method: {nameof(GetCurrentUserWeight)}");
                return (ErrorResponse.Internal(ex.Message), 0);
            }
        }

        public async Task<(ErrorResponse error, int value)> GetCurrentlyBurntCaloriesValueForUser(string userId, DateTime date)
        {
            try
            {
                var userStatisticDoc = await _userStatisticsDocument.Find(a=>a.UserId == userId).FirstOrDefaultAsync();
                if(userStatisticDoc == null)
                {
                    await _helperService.CreateMissingDoc(userId, _userStatisticsDocument);
                    _logger.LogError($"User statistics document not found. UserId: {userId} Method: {nameof(GetCurrentlyBurntCaloriesValueForUser)}");
                    return (ErrorResponse.Ok(), 0);
                }

                var caloriesStatistics = userStatisticDoc.UserStatisticGroups.FirstOrDefault(a => a.Type == StatisticType.CaloriesBurnt);
                if(caloriesStatistics == null) { return (ErrorResponse.Ok(), 0); }

                var todayCalories = caloriesStatistics.Records.FirstOrDefault(a=>a.Date == date);
                if(todayCalories == null) { return (ErrorResponse.Ok(), 0); }

                return (ErrorResponse.Ok(), (int)todayCalories.Value);
            }
            catch(Exception ex)
            {
                return (ErrorResponse.Internal(ex.Message), 0);
            }
        }

        public async Task<(ErrorResponse error, double water)> GetCurrentWaterIntake(string userId, DateTime date)
        {
            try
            {
                var dailyDietDoc = await _dailyDietCollection.Find(a=>a.UserId == userId).FirstOrDefaultAsync();
                if(dailyDietDoc == null)
                {
                    _logger.LogWarning($"user {userId} daily diet collection does not exist. creating.");
                    var newDoc = await _helperService.CreateMissingDoc(userId, _dailyDietCollection);
                    if(newDoc == null)
                    {
                        return (ErrorResponse.NotFound("User daily diet document not found."), 0);
                    }

                    return (ErrorResponse.Ok(), 0);
                }

                double waterIntake = 0;

                var targetDay = dailyDietDoc.DailyPlans.FirstOrDefault(a => a.Date.Date == date.Date);
                if(targetDay != null)
                {
                    waterIntake = targetDay.Water;
                }

                return (ErrorResponse.Ok(), waterIntake);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Error while trying to get current water intake for user. UserId: {userId} Date: {date} Method: {nameof(GetCurrentWaterIntake)}");
                return (ErrorResponse.Internal(ex.Message), 0);
            }
        }

        public async Task<(ErrorResponse error, UserLayoutVMO? data)> GetUserLayout(string userId)
        {
            try
            {
                var user = await _dbContext.AppUser.FirstOrDefaultAsync(a=>a.Id == userId);
                if (user == null)
                {
                    _logger.LogCritical($"User not found. UserId: {userId} Method: {nameof(GetUserLayout)}");
                    return (ErrorResponse.NotFound(), null);
                }

                if(user.LayoutSettings == null)
                {
                    user.LayoutSettings = new LayoutSettings
                    {
                        Animations = true,
                        ChartStack = new List<ChartStack>
                        {
                            new ChartStack
                            {
                                ChartType = ChartType.Linear,
                                ChartDataType = ChartDataType.Exercise,
                                Period = Period.All,
                                Name = "Benchpress"
                            },
                            new ChartStack
                            {
                                ChartType = ChartType.Compare,
                                ChartDataType = ChartDataType.Exercise,
                                Period = Period.Last,
                                Name = "Benchpress"
                            },
                            new ChartStack
                            {
                                ChartType = ChartType.Hexagonal,
                                ChartDataType = ChartDataType.NotDefined,
                                Period = Period.Week,
                                Name = "Muscle engagement"
                            },
                            new ChartStack
                            {
                                ChartType = ChartType.Bar,
                                ChartDataType = ChartDataType.Calorie,
                                Period = Period.Last5,
                                Name = "Calories"
                            },
                            new ChartStack
                            {
                                ChartType = ChartType.Circle,
                                ChartDataType = ChartDataType.MakroDist,
                                Period = Period.Last,
                                Name = "Daily makro"
                            }
                        }
                    };

                    await _dbContext.SaveChangesAsync();
                }

                return (ErrorResponse.Ok(), ConvertToUserLayoutVMO(user.LayoutSettings));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get user layout. UserId: {userId} Method: {nameof(GetUserLayout)}");
                return (ErrorResponse.Internal(ex.Message), null);
            }
        }

        public async Task<(ErrorResponse error, ExercisePastDataVMO? data)> GetPastExerciseData(string userId, string exerciseName, string period = "all")
        {
            try
            {
                if (period != "all" && period != "year" && period != "month" && period != "week")
                {
                    _logger.LogWarning($"User tried to use diffrent period than expected. UserId {userId} PeriodUsed: {period} Method: {nameof(ExercisePastDataVMO)}");
                    return (ErrorResponse.StateNotValid<string>($"Invalid period: {period}. Allowed values are 'all', 'year', 'month', 'week'."), null);
                }

                var userPastExercisesDoc = await _exercisesHistoryCollection.Find(a=>a.UserId == userId).FirstOrDefaultAsync();
                if(userPastExercisesDoc == null)
                {
                    _logger.LogWarning($"User past exercise document not found. UserId: {userId} Method: {nameof(GetPastExerciseData)}");

                    var newDoc = await _helperService.CreateMissingDoc(userId, _exercisesHistoryCollection);
                    if(newDoc == null)
                    {
                        return (ErrorResponse.NotFound("User exercise history document not found."), null);
                    }

                    return (ErrorResponse.Ok($"Correctly retrived {exerciseName} data but document is empty."), new ExercisePastDataVMO() { ExerciseName = exerciseName});
                }

                var targetedExercise = userPastExercisesDoc.ExerciseHistoryLists.FirstOrDefault(a=>a.ExerciseName == exerciseName);
                if(targetedExercise == null)
                {
                    return (ErrorResponse.Ok($"No past data for {exerciseName} found."), new ExercisePastDataVMO() { ExerciseName = exerciseName});
                }

                ExercisePastDataVMO exercisePastDataVMO = new ExercisePastDataVMO() { ExerciseName = exerciseName};
                var exData = targetedExercise.ExerciseData;

                switch (period)
                {
                    case "year":
                        exData = targetedExercise.ExerciseData.Where(a => a.Date >= DateTime.Now.AddYears(-1)).ToList();
                        break;
                    case "month":
                        exData = targetedExercise.ExerciseData.Where(a => a.Date >= DateTime.Now.AddMonths(-1)).ToList();
                        break;
                    case "week":
                        exData = targetedExercise.ExerciseData.Where(a => a.Date >= DateTime.Now.AddDays(-7)).ToList();
                        break;
                }

                exercisePastDataVMO.PastData.AddRange(
                    exData.Select(a => new ExercisePastData
                    {
                        Date = a.Date,
                        Series = a.Series.Where(serie => serie.Repetitions != 0).Select(serie => new ExercisePastSerieData
                        {
                            Repetitions = serie.Repetitions,
                            WeightKg = serie.WeightKg,
                            WeightLbs = serie.WeightLbs
                        }).ToList()
                    })
                );

                exercisePastDataVMO.PastData.RemoveAll(x => x.Series.Count == 0);

                return (ErrorResponse.Ok(), exercisePastDataVMO);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get past data of an exercise. UserId: {userId} ExerciseName: {exerciseName} Method: {nameof(GetPastExerciseData)}");
                return (ErrorResponse.Internal(ex.Message), null);
            }
        }

        //prv
        private UserLayoutVMO ConvertToUserLayoutVMO(LayoutSettings layoutSettings)
        {
            return new UserLayoutVMO
            {
                Animations = layoutSettings.Animations,
                ChartStack = layoutSettings.ChartStack.Select(cs => new ChartStackVMO
                {
                    ChartType = cs.ChartType,
                    ChartDataType = cs.ChartDataType,
                    Period = cs.Period,
                    Name = cs.Name
                }).ToList()
            };
        }

        public async Task<(ErrorResponse error, MuscleUsageDataVMO? data)> GetMuscleUsageData(string userId, string period = "all")
        {
            try
            {
                var userExercisesDataDoc = await _exercisesHistoryCollection.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if (userExercisesDataDoc == null)
                {
                    _logger.LogWarning($"user {userId} exercise history collection does not exist. creating.");
                    await _helperService.CreateMissingDoc(userId, _exercisesHistoryCollection);
                    return (ErrorResponse.Ok(), new MuscleUsageDataVMO());
                }

                var vmo = new MuscleUsageDataVMO();

                switch (period.ToLower())
                {
                    case "all":
                        foreach (var exercise in userExercisesDataDoc.ExerciseHistoryLists)
                        {
                            var filteredDates = exercise.ExerciseData.Select(a => a.Date).ToList();

                            if (filteredDates.Count == 0) continue;

                            var existingMuscleUsage = vmo.muscleUsage.FirstOrDefault(mu => mu.MuscleType == exercise.MuscleType);

                            if (existingMuscleUsage != null)
                            {
                                existingMuscleUsage.Dates.AddRange(filteredDates);
                            }
                            else
                            {
                                vmo.muscleUsage.Add(new MuscleUsage
                                {
                                    MuscleType = exercise.MuscleType,
                                    Dates = filteredDates
                                });
                            }
                        }
                        break;

                    case "year":
                        foreach (var exercise in userExercisesDataDoc.ExerciseHistoryLists)
                        {
                            var filteredDates = exercise.ExerciseData.Where(a => a.Date >= DateTime.Now.AddYears(-1)).Select(a => a.Date).ToList();

                            if (filteredDates.Count == 0) continue;

                            var existingMuscleUsage = vmo.muscleUsage.FirstOrDefault(mu => mu.MuscleType == exercise.MuscleType);

                            if (existingMuscleUsage != null)
                            {
                                existingMuscleUsage.Dates.AddRange(filteredDates);
                            }
                            else
                            {
                                vmo.muscleUsage.Add(new MuscleUsage
                                {
                                    MuscleType = exercise.MuscleType,
                                    Dates = filteredDates
                                });
                            }
                        }
                        break;

                    case "month":
                        foreach (var exercise in userExercisesDataDoc.ExerciseHistoryLists)
                        {
                            var filteredDates = exercise.ExerciseData.Where(a => a.Date >= DateTime.Now.AddMonths(-1)).Select(a => a.Date).ToList();

                            if (filteredDates.Count == 0) continue;

                            var existingMuscleUsage = vmo.muscleUsage.FirstOrDefault(mu => mu.MuscleType == exercise.MuscleType);

                            if (existingMuscleUsage != null)
                            {
                                existingMuscleUsage.Dates.AddRange(filteredDates);
                            }
                            else
                            {
                                vmo.muscleUsage.Add(new MuscleUsage
                                {
                                    MuscleType = exercise.MuscleType,
                                    Dates = filteredDates
                                });
                            }
                        }
                        break;

                    case "week":
                        foreach (var exercise in userExercisesDataDoc.ExerciseHistoryLists)
                        {
                            var filteredDates = exercise.ExerciseData.Where(a => a.Date >= DateTime.Now.AddWeeks(-1)).Select(a => a.Date).ToList();

                            if (filteredDates.Count == 0) continue;

                            var existingMuscleUsage = vmo.muscleUsage.FirstOrDefault(mu => mu.MuscleType == exercise.MuscleType);

                            if (existingMuscleUsage != null)
                            {
                                existingMuscleUsage.Dates.AddRange(filteredDates);
                            }
                            else
                            {
                                vmo.muscleUsage.Add(new MuscleUsage
                                {
                                    MuscleType = exercise.MuscleType,
                                    Dates = filteredDates
                                });
                            }
                        }
                        break;

                    default:
                        foreach (var exercise in userExercisesDataDoc.ExerciseHistoryLists)
                        {
                            var filteredDates = exercise.ExerciseData.Select(a => a.Date).ToList();

                            if (filteredDates.Count == 0) continue;

                            var existingMuscleUsage = vmo.muscleUsage.FirstOrDefault(mu => mu.MuscleType == exercise.MuscleType);

                            if (existingMuscleUsage != null)
                            {
                                existingMuscleUsage.Dates.AddRange(filteredDates);
                            }
                            else
                            {
                                vmo.muscleUsage.Add(new MuscleUsage
                                {
                                    MuscleType = exercise.MuscleType,
                                    Dates = filteredDates
                                });
                            }
                        }
                        break;
                }

                return (ErrorResponse.Ok(), vmo);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get muscle usage data UserId: {userId} Period: {period} Method: {nameof(GetMuscleUsageData)}");
                return (ErrorResponse.Internal(ex.Message), null);
            }
        }

        public async Task<(ErrorResponse error, MakroDataVMO? data)> GetPastMakroData(string userId, string period = "all")
        {
            try
            {
                var dietHistoryDocument = await _dietHistoryCollection.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if (dietHistoryDocument == null)
                {
                    _logger.LogWarning($"user {userId} diet history collection does not exist. creating...");
                    await _helperService.CreateMissingDoc(userId, _dietHistoryCollection);
                    return (ErrorResponse.Ok(), new MakroDataVMO());
                }

                var vmo = new MakroDataVMO();

                switch (period.ToLower())
                {
                    case "all":
                        foreach (var day in dietHistoryDocument.DailyPlans)
                        {
                            MakroData data = new MakroData();
                            data.Date = day.Date;

                            foreach (var meal in day.Meals)
                            {
                                foreach (var ing in meal.Ingridient)
                                {
                                    data.Proteins += ((ing.Proteins * ing.WeightValue) / ing.PrepedFor);
                                    data.Carbs += ((ing.Carbs * ing.WeightValue) / ing.PrepedFor);
                                    data.EnergyKj += ((ing.EnergyKj * ing.WeightValue) / ing.PrepedFor);
                                    data.Fats += ((ing.Fats * ing.WeightValue) / ing.PrepedFor);
                                    data.EnergyKcal += ((ing.EnergyKcal * ing.WeightValue) / ing.PrepedFor);
                                }
                            }

                            vmo.MakroData.Add(data);
                        }
                        break;
                    case "year":
                        var cutoff = DateTime.Now.AddYears(-1);
                        foreach (var day in dietHistoryDocument.DailyPlans.Where(d => d.Date >= cutoff))
                        {
                            MakroData data = new MakroData();
                            data.Date = day.Date;
                            foreach (var meal in day.Meals)
                            {
                                foreach (var ing in meal.Ingridient)
                                {
                                    data.Proteins += ((ing.Proteins * ing.WeightValue) / ing.PrepedFor);
                                    data.Carbs += ((ing.Carbs * ing.WeightValue) / ing.PrepedFor);
                                    data.EnergyKj += ((ing.EnergyKj * ing.WeightValue) / ing.PrepedFor);
                                    data.Fats += ((ing.Fats * ing.WeightValue) / ing.PrepedFor);
                                    data.EnergyKcal += ((ing.EnergyKcal * ing.WeightValue) / ing.PrepedFor);
                                }
                            }
                            vmo.MakroData.Add(data);
                        }
                        break;
                    case "month":
                        var cutoffMonth = DateTime.Now.AddMonths(-1);
                        foreach (var day in dietHistoryDocument.DailyPlans.Where(d => d.Date >= cutoffMonth))
                        {
                            MakroData data = new MakroData();
                            data.Date = day.Date;
                            foreach (var meal in day.Meals)
                            {
                                foreach (var ing in meal.Ingridient)
                                {
                                    data.Proteins += ((ing.Proteins * ing.WeightValue) / ing.PrepedFor);
                                    data.Carbs += ((ing.Carbs * ing.WeightValue) / ing.PrepedFor);
                                    data.EnergyKj += ((ing.EnergyKj * ing.WeightValue) / ing.PrepedFor);
                                    data.Fats += ((ing.Fats * ing.WeightValue) / ing.PrepedFor);
                                    data.EnergyKcal += ((ing.EnergyKcal * ing.WeightValue) / ing.PrepedFor);
                                }
                            }
                            vmo.MakroData.Add(data);
                        }
                        break;
                    case "week":
                        var cutoffWeek = DateTime.Now.AddDays(-7);
                        foreach (var day in dietHistoryDocument.DailyPlans.Where(d => d.Date >= cutoffWeek))
                        {
                            MakroData data = new MakroData();
                            data.Date = day.Date;
                            foreach (var meal in day.Meals)
                            {
                                foreach (var ing in meal.Ingridient)
                                {
                                    data.Proteins += ((ing.Proteins * ing.WeightValue) / ing.PrepedFor);
                                    data.Carbs += ((ing.Carbs * ing.WeightValue) / ing.PrepedFor);
                                    data.EnergyKj += ((ing.EnergyKj * ing.WeightValue) / ing.PrepedFor);
                                    data.Fats += ((ing.Fats * ing.WeightValue) / ing.PrepedFor);
                                    data.EnergyKcal += ((ing.EnergyKcal * ing.WeightValue) / ing.PrepedFor);
                                }
                            }
                            vmo.MakroData.Add(data);
                        }
                        break;
                }

                var currentWeekData = await _dailyDietCollection.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if (currentWeekData == null)
                {
                    _logger.LogWarning($"user {userId} diet collection does not exist. creating...");
                    await _helperService.CreateMissingDoc(userId, _dailyDietCollection);
                }

                foreach (var day in currentWeekData.DailyPlans)
                {
                    MakroData data = new MakroData();
                    data.Date = day.Date;

                    foreach (var meal in day.Meals)
                    {
                        foreach(var ing in meal.Ingridient)
                        {
                            data.Proteins += ((ing.Proteins * ing.WeightValue) / ing.PrepedFor);
                            data.Carbs += ((ing.Carbs * ing.WeightValue) / ing.PrepedFor);
                            data.EnergyKj += ((ing.EnergyKj * ing.WeightValue) / ing.PrepedFor);
                            data.Fats += ((ing.Fats * ing.WeightValue) / ing.PrepedFor);
                            data.EnergyKcal += ((ing.EnergyKcal * ing.WeightValue) / ing.PrepedFor);
                        }
                    }

                    vmo.MakroData.Add(data);
                }

                return (ErrorResponse.Ok(), vmo);
            } 
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get past makro data. UserId: {userId} Period: {period} Method: {nameof(GetPastMakroData)}");
                return (ErrorResponse.Internal(ex.Message), null);
            }
        }

        public async Task<(ErrorResponse error, DailyMakroDistributionVMO? data)> GetDailyMakroDisturbtion(string userId, DateTime date)
        {
            try
            {
                var dailyDietDocument = await _dailyDietCollection.Find(a=>a.UserId == userId).FirstOrDefaultAsync();
                if (dailyDietDocument == null)
                {
                    _logger.LogWarning($"user {userId} daily diet collection does not exist. creating...");
                    var newDoc = await _helperService.CreateMissingDoc(userId, _dailyDietCollection);
                    if (newDoc != null)
                    {
                        return (ErrorResponse.Ok(), new DailyMakroDistributionVMO() { Date = date });
                    }

                    return (ErrorResponse.NotFound("User diet document not found."), null);
                }

                var vmo = new DailyMakroDistributionVMO() { Date = date };

                var targetDay = dailyDietDocument.DailyPlans.FirstOrDefault(a => a.Date == date);
                if (targetDay != null)
                {
                    foreach (var meal in targetDay.Meals)
                    {
                        var mealRec = new DailyDistributionMeals() { Name = meal.Name, Distribution = new DailyDistribution() };                       

                        foreach(var ing in meal.Ingridient)
                        {
                            double distProtein = (ing.Proteins * ing.WeightValue) / ing.PrepedFor;
                            double distFats = (ing.Fats * ing.WeightValue) / ing.PrepedFor;
                            double distCarbs = (ing.Carbs * ing.WeightValue) / ing.PrepedFor;
                            double distKcal = (ing.EnergyKcal * ing.WeightValue) / ing.PrepedFor;

                            var ingRec = new DailyDistributionIngridient()
                            {
                                Name = ing.Name,
                                Grams = ing.WeightValue,
                                Distribution = new DailyDistribution()
                                {
                                    Protein = distProtein,
                                    Fats = distFats,
                                    Carbs = distCarbs,
                                    Kcal = distKcal,
                                }                           
                            };

                            mealRec.Distribution.Kcal += distKcal;
                            mealRec.Distribution.Carbs += distCarbs;
                            mealRec.Distribution.Fats += distFats;
                            mealRec.Distribution.Protein += distProtein;
                            mealRec.Ingridients.Add(ingRec);
                        }
                        vmo.Meals.Add(mealRec);
                    }
                }

                return (ErrorResponse.Ok(), vmo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get daily makro disturbtion for user. UserId: {userId} Datae: {date} Method: {nameof(GetDailyMakroDisturbtion)}");
                return (ErrorResponse.Internal(ex.Message), null);
            }
        }

        public async Task<(ErrorResponse error, UserWeightHistoryVMO? data)> GetUserWeightHistory(string userId)
        {
            try
            {
                var userStatisticsDoc = await _userStatisticsDocument.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if (userStatisticsDoc == null)
                {
                    var newDoc = await _helperService.CreateMissingDoc(userId, _userStatisticsDocument);
                    if (newDoc == null)
                    {
                        _logger.LogCritical($"User statistics document not found. UserId: {userId}");
                        return (ErrorResponse.Failed(), null);
                    }

                    return (ErrorResponse.Ok(), new UserWeightHistoryVMO());
                }

                var weightGroup = userStatisticsDoc.UserStatisticGroups.FirstOrDefault(g => g.Type == StatisticType.Weight);
                if(weightGroup == null)
                {
                    return (ErrorResponse.Ok(), new UserWeightHistoryVMO());
                }

                var vm = new UserWeightHistoryVMO();
                if (weightGroup != null && weightGroup.Records.Any())
                {
                    vm.Records = weightGroup.Records.OrderBy(r => r.Date)
                       .Select(r => new WeightRecord
                       {
                           Date = r.Date,
                           Weight = r.Value
                       })
                       .ToList();
                }

                return (ErrorResponse.Ok(), vm);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get user weight history. UserId: {userId}");
                return (ErrorResponse.Internal(ex.Message), null);
            }
        }

        public async Task<ErrorResponse> UpdateLayout(string userId, UserLayoutVM model)
        {
            try
            {
                var user = await _dbContext.AppUser.FirstOrDefaultAsync(a => a.Id == userId);

                if (user == null)
                    return ErrorResponse.NotFound("User not found.");

                var settings = user.LayoutSettings ?? new LayoutSettings();

                settings.Animations = model.Animations;

                if (model.ChartStack != null && model.ChartStack.Any())
                {
                    settings.ChartStack = model.ChartStack;
                }

                user.LayoutSettings = settings;

                _dbContext.AppUser.Update(user);
                await _dbContext.SaveChangesAsync();

                return ErrorResponse.Ok();
            } 
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to update user layout. UserId: {userId} Data: {model} Method: {nameof(UpdateLayout)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<ErrorResponse> AddToUserStatistics(string userId, List<UserStatisticsVM> model, IClientSessionHandle session = null, bool caloriesNormal = false)
        {
            try
            {
                UserStatisticsDocument userStatisticsDoc;
                if (session == null)
                {
                    userStatisticsDoc = await _userStatisticsDocument.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                }
                else
                {
                    userStatisticsDoc = await _userStatisticsDocument.Find(session, a => a.UserId == userId).FirstOrDefaultAsync();
                }

                if(userStatisticsDoc == null)
                {
                    _logger.LogWarning($"user {userId} statistics collection does not exist. creating...");
                    var newDoc = await _helperService.CreateMissingDoc(userId, _userStatisticsDocument);
                    if (newDoc == null)
                    {
                        return ErrorResponse.NotFound("Coudn't save user statistics. Document not found.");
                    }

                    userStatisticsDoc = newDoc;
                }

                foreach(var item in model)
                {
                    var group = userStatisticsDoc.UserStatisticGroups.FirstOrDefault(g => g.Type == item.Type);
                    if (group == null)
                    {
                        group = new UserStatisticGroup
                        {
                            Type = item.Type,
                            Records = new List<UserStatisticRecord>()
                        };
                        userStatisticsDoc.UserStatisticGroups.Add(group);
                    }

                    var existingRecord = group.Records.FirstOrDefault(r => r.Date.Date == item.Date.Date);

                    switch (item.Type)
                    {
                        case StatisticType.CaloriesBurnt:
                            {
                                double newCalories = item.Value;

                                if (existingRecord != null)
                                {
                                    if (caloriesNormal)
                                    {
                                        existingRecord.Value += newCalories;
                                        userStatisticsDoc.TotalCaloriesCounter += newCalories;
                                    }
                                    else
                                    {
                                        double oldCalories = existingRecord.Value;
                                        double delta = newCalories - oldCalories;

                                        if (delta > 0)
                                        {
                                            existingRecord.Value = newCalories;
                                            userStatisticsDoc.TotalCaloriesCounter += delta;
                                        }
                                    }
                                }
                                else
                                {
                                    var record = new UserStatisticRecord
                                    {
                                        Date = item.Date,
                                        Value = newCalories
                                    };
                                    group.Records.Add(record);
                                    userStatisticsDoc.TotalCaloriesCounter += newCalories;
                                }
                            }
                            break;

                        case StatisticType.StepsTaken:
                            {
                                int newSteps = (int)item.Value;

                                if (existingRecord != null)
                                {
                                    int oldSteps = (int)existingRecord.Value;
                                    int delta = newSteps - oldSteps;
                                    if (delta > 0)
                                    {
                                        existingRecord.Value = newSteps;
                                        userStatisticsDoc.TotalStepsCounter += delta;
                                    }
                                }
                                else
                                {
                                    var record = new UserStatisticRecord
                                    {
                                        Date = item.Date,
                                        Value = newSteps
                                    };
                                    group.Records.Add(record);
                                    userStatisticsDoc.TotalStepsCounter += newSteps;
                                }
                            }
                            break;

                        case StatisticType.TimeSpend:
                            {
                                var change = item.TimeValue ?? TimeSpan.Zero;

                                if (existingRecord != null)
                                {
                                    var oldTime = existingRecord.TimeValue;
                                    var newTime = oldTime + change;
                                    existingRecord.TimeValue = newTime;

                                    userStatisticsDoc.TotalTimeSpend += change;
                                }
                                else
                                {
                                    var record = new UserStatisticRecord
                                    {
                                        Date = item.Date,
                                        TimeValue = change
                                    };
                                    group.Records.Add(record);

                                    userStatisticsDoc.TotalTimeSpend += change;
                                }
                            }
                            break;

                        case StatisticType.ActvSessionsCount:
                            {
                                int changeSessions = (int)item.Value;

                                if (existingRecord != null)
                                {
                                    int oldCount = (int)existingRecord.Value;
                                    int newCount = oldCount + changeSessions;
                                    existingRecord.Value = newCount;

                                    userStatisticsDoc.TotalSessionsCounter += changeSessions;
                                }
                                else
                                {
                                    var record = new UserStatisticRecord
                                    {
                                        Date = item.Date,
                                        Value = changeSessions
                                    };
                                    group.Records.Add(record);

                                    userStatisticsDoc.TotalSessionsCounter += changeSessions;
                                }
                            }
                            break;

                        case StatisticType.WeightLifted:
                            {
                                double changeKg = item.Value;

                                if (existingRecord != null)
                                {
                                    existingRecord.Value = changeKg;
                                    userStatisticsDoc.TotalWeightLiftedCounter = changeKg;
                                }
                                else
                                {
                                    var record = new UserStatisticRecord
                                    {
                                        Date = item.Date,
                                        Value = changeKg
                                    };
                                    group.Records.Add(record);

                                    userStatisticsDoc.TotalWeightLiftedCounter += changeKg;
                                }
                            }
                            break;

                        case StatisticType.TotalDistance:
                            {
                                double changeDistance = item.Value;

                                if (existingRecord != null)
                                {
                                    double oldDistance = existingRecord.Value;
                                    double newDistance = oldDistance + changeDistance;
                                    existingRecord.Value = newDistance;

                                    userStatisticsDoc.TotalDistanceCounter += changeDistance;
                                }
                                else
                                {
                                    var record = new UserStatisticRecord
                                    {
                                        Date = item.Date,
                                        Value = changeDistance
                                    };
                                    group.Records.Add(record);

                                    userStatisticsDoc.TotalDistanceCounter += changeDistance;
                                }
                            }
                            break;
                    }
                }

                if (session == null)
                {
                    await _userStatisticsDocument.ReplaceOneAsync(d => d.UserId == userId, userStatisticsDoc);
                }
                else
                {
                    await _userStatisticsDocument.ReplaceOneAsync(session, d => d.UserId == userId, userStatisticsDoc);
                }

                return ErrorResponse.Ok();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to add statistics to user statistics doc. UserId: {userId} Model: {model} Method: {nameof(AddToUserStatistics)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<ErrorResponse> AddWeight(string userId, AddWeightVM model)
        {
            try
            {
                var userStatisticsDoc = await _userStatisticsDocument.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if(userStatisticsDoc == null)
                {
                    var newDoc = await _helperService.CreateMissingDoc(userId, _userStatisticsDocument);
                    if(newDoc == null)
                    {
                        _logger.LogCritical($"User statistics document not found. UserId: {userId}");
                        return ErrorResponse.NotFound("User statistics document not found.");
                    }

                    userStatisticsDoc = newDoc;
                }

                var weightGroup = userStatisticsDoc.UserStatisticGroups.Find(a=>a.Type == StatisticType.Weight);
                if (weightGroup == null)
                {
                    weightGroup = new UserStatisticGroup
                    {
                        Type = StatisticType.Weight,
                        Records = new List<UserStatisticRecord>() { new UserStatisticRecord() { Date = model.Date, Value = model.Weight } }
                    };

                    userStatisticsDoc.UserStatisticGroups.Add(weightGroup);
                }

                var alreadyExistingRecord = weightGroup.Records.Find(r => r.Date.Date == model.Date.Date);
                if (alreadyExistingRecord == null)
                {
                    weightGroup.Records.Add(new UserStatisticRecord()
                    {
                        Date = model.Date,
                        Value = model.Weight
                    });
                }
                else
                {
                    alreadyExistingRecord.Value = model.Weight;
                }

                await _userStatisticsDocument.ReplaceOneAsync(d => d.UserId == userId, userStatisticsDoc);
                return ErrorResponse.Ok();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to add weight for user. UserId: {userId} Method: {nameof(AddWeight)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<(ErrorResponse error, string? newPfpUrl)> UpdateProfileInformation(string userId, UserProfileInformationVM model)
        {
            try
            {
                var user = await _dbContext.AppUser.FirstOrDefaultAsync(a => a.Id == userId);
                if(user == null)
                {
                    _logger.LogWarning($"User with id not found -> UserId: {userId} Method: {nameof(UpdateProfileInformation)}");
                    return (ErrorResponse.NotFound("User with given id not found."), null);
                }

                if (!string.IsNullOrEmpty(model.NewDesc))
                {
                    user.Desc = model.NewDesc;
                }

                if (!string.IsNullOrEmpty(model.NewName))
                {
                    user.Name = model.NewName;
                }

                if (model.IsVisible != null)
                {
                    user.IsProfilePrivate = (bool)model.IsVisible;
                }

                if (model.NewImage != null)
                {
                    string oldImage = user.Pfp;

                    Random rnd = new Random();
                    string extension = Path.GetExtension(model.NewImage.FileName);
                    extension = string.IsNullOrEmpty(extension) ? "jpg" : extension.TrimStart('.');
                    string newImageName = $"{Guid.NewGuid()}{rnd.Next(1, 1000000)}.{extension}";

                    string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets/Images/UserPfp");

                    Directory.CreateDirectory(folderPath);

                    string fullPath = Path.Combine(folderPath, newImageName);

                    using (var fileStream = new FileStream(fullPath, FileMode.Create))
                    {
                        await model.NewImage.CopyToAsync(fileStream);
                    }

                    string defImage = "/pfp-images/e2f56642-a493-4c6d-924b-d3072714646a.png";

                    if (!string.IsNullOrEmpty(oldImage) && !oldImage.Equals(defImage, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            string oldFileName = Path.GetFileName(oldImage);
                            string oldFullPath = Path.Combine(folderPath, oldFileName);

                            if (File.Exists(oldFullPath))
                                    File.Delete(oldFullPath);
                        }
                        catch (Exception deleteEx)
                        {
                            _logger.LogWarning(deleteEx, $"Failed to delete old profile image {oldImage} for user {userId}");
                        }
                    }

                    user.Pfp = $"/pfp-images/{newImageName}";
                }

                await _dbContext.SaveChangesAsync();
                return (ErrorResponse.Ok(), user.Pfp);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to update profile information for user. UserId: {userId} Method: {nameof(UpdateProfileInformation)}");
                return (ErrorResponse.Internal(ex.Message), null);
            }
        }

        public async Task<ErrorResponse> ChangeProfileVisilibity(string userId)
        {
            try
            {
                var user = await _dbContext.AppUser.FirstOrDefaultAsync(a => a.Id == userId);
                if (user == null)
                {
                    _logger.LogWarning($"User with id not found -> UserId: {userId} Method: {nameof(UpdateProfileInformation)}");
                    return ErrorResponse.NotFound("User with given id not found.");
                }

                user.IsProfilePrivate = !user.IsProfilePrivate;
                await _dbContext.SaveChangesAsync();
                return ErrorResponse.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to chane profile visilibiyu for user. UserId: {userId} Method: {nameof(ChangeProfileVisilibity)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<ErrorResponse> UpdateUserStepsTreshold(string userId, int newTreshold)
        {
            try
            {
                var user = await _dbContext.AppUser.FirstOrDefaultAsync(a => a.Id == userId);
                if(user == null)
                {
                    return ErrorResponse.NotFound();
                }

                user.StepsThreshold = newTreshold;
                await _dbContext.SaveChangesAsync();

                return ErrorResponse.Ok();
            }
            catch(Exception ex)
            {
                return ErrorResponse.Internal(ex.Message);
            }
        }
    }
}
