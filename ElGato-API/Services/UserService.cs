using ElGato_API.Data;
using ElGato_API.Interfaces;
using ElGato_API.Models.User;
using ElGato_API.ModelsMongo.Diet;
using ElGato_API.ModelsMongo.History;
using ElGato_API.VM.UserData;
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
        private readonly IHelperService _helperService;
        public UserService(AppDbContext dbContext, ILogger<UserService> logger, IMongoDatabase database, IHelperService helperService) 
        { 
            _dbContext = dbContext;
            _logger = logger;
            _exercisesHistoryCollection = database.GetCollection<ExercisesHistoryDocument>("ExercisesHistory");
            _dietHistoryCollection = database.GetCollection<DietHistoryDocument>("DietHistory");
            _dailyDietCollection = database.GetCollection<DietDocument>("DailyDiet");
            _helperService = helperService;
        }

        public async Task<(BasicErrorResponse error, string? data)> GetSystem(string userId)
        {
            try
            {
                var res = await _dbContext.AppUser.FirstOrDefaultAsync(a=>a.Id == userId);
                if(res == null)
                {
                    return (new BasicErrorResponse() { ErrorCode = ErrorCodes.NotFound, ErrorMessage = $"user with specified id not found", Success = false }, null);
                }

                return (new BasicErrorResponse() { Success = true, ErrorCode = ErrorCodes.None }, res.Metric ? "metric" : "imperial");
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, $"Failed while trying to get system. UserId: {userId} Method: {nameof(GetSystem)}");
                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"something went wrong, {ex.Message} ", Success = false}, null);
            }            
        }

        public async Task<(BasicErrorResponse error, UserCalorieIntake model)> GetUserCalories(string userId)
        {
            BasicErrorResponse error = new BasicErrorResponse() { Success = false };
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

        public async Task<(BasicErrorResponse error, UserCalorieIntake? model)> GetCurrentCalories(string userId, DateTime date)
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
                        return (new BasicErrorResponse() { Success = false, ErrorCode = ErrorCodes.NotFound, ErrorMessage = $"User daily diet collection not found." }, null);
                    }

                    return (new BasicErrorResponse() { Success = true, ErrorCode = ErrorCodes.None, ErrorMessage = $"Sucess" }, new UserCalorieIntake());
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

                return (new BasicErrorResponse() { Success = true, ErrorCode = ErrorCodes.None, ErrorMessage = "Sucess" }, vmo);

            }
            catch(Exception ex)
            {
                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"Error occured: {ex.Message}", Success = false }, null);
            }
        }

        public async Task<(BasicErrorResponse error, double weight)> GetCurrentUserWeight(string userId)
        {
            try
            {
                var res = await _dbContext.AppUser.Include(a=>a.UserInformation).FirstOrDefaultAsync(a=>a.Id == userId);
                if(res == null || res.UserInformation == null)
                {
                    _logger.LogWarning($"User record with id: {userId} not found while trying to get user current weight. Method: {nameof(GetCurrentUserWeight)}");
                    return (new BasicErrorResponse() { ErrorCode = ErrorCodes.NotFound, ErrorMessage = "User with given id and its weight not found.", Success = false }, 0);
                }

                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.None, Success = true, ErrorMessage = "Sucess"}, res.UserInformation.Weight ?? 0);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Error occured while trying to get current user weight UserId: {userId} Method: {nameof(GetCurrentUserWeight)}");
                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"Error occured: {ex}", Success = false}, 0);
            }
        }

        public async Task<(BasicErrorResponse error, double water)> GetCurrentWaterIntake(string userId, DateTime date)
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
                        return (new BasicErrorResponse() { ErrorCode = ErrorCodes.NotFound, ErrorMessage = "User daily diet document not found.", Success = false }, 0);
                    }

                    return (new BasicErrorResponse() { ErrorCode = ErrorCodes.None, ErrorMessage = "Sucess", Success = true }, 0);
                }

                double waterIntake = 0;

                var targetDay = dailyDietDoc.DailyPlans.FirstOrDefault(a => a.Date.Date == date.Date);
                if(targetDay != null)
                {
                    waterIntake = targetDay.Water;
                }

                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.None, Success = true, ErrorMessage = "Sucesss" }, waterIntake);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Error while trying to get current water intake for user. UserId: {userId} Date: {date} Method: {nameof(GetCurrentWaterIntake)}");
                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"Error occured: {ex.Message}", Success = false }, 0);
            }
        }

        public async Task<(BasicErrorResponse error, UserLayoutVMO? data)> GetUserLayout(string userId)
        {
            try
            {
                var user = await _dbContext.AppUser.FirstOrDefaultAsync(a=>a.Id == userId);
                if (user == null)
                {
                    _logger.LogCritical($"User not found. UserId: {userId} Method: {nameof(GetUserLayout)}");
                    return (new BasicErrorResponse() { ErrorCode = ErrorCodes.NotFound, ErrorMessage = "User not found.", Success = false }, null);
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

                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.None, ErrorMessage = "Sucess", Success = true }, ConvertToUserLayoutVMO(user.LayoutSettings));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get user layout. UserId: {userId} Method: {nameof(GetUserLayout)}");
                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = ex.Message, Success = false }, null);
            }
        }

        public async Task<(BasicErrorResponse error, ExercisePastDataVMO? data)> GetPastExerciseData(string userId, string exerciseName, string period = "all")
        {
            try
            {
                if (period != "all" && period != "year" && period != "month" && period != "week")
                {
                    _logger.LogWarning($"User tried to use diffrent period than expected. UserId {userId} PeriodUsed: {period} Method: {nameof(ExercisePastDataVMO)}");
                    return (new BasicErrorResponse() { ErrorCode = ErrorCodes.ModelStateNotValid, ErrorMessage = $"Invalid period: {period}. Allowed values are 'all', 'year', 'month', 'week'.", Success = false}, null);
                }

                var userPastExercisesDoc = await _exercisesHistoryCollection.Find(a=>a.UserId == userId).FirstOrDefaultAsync();
                if(userPastExercisesDoc == null)
                {
                    _logger.LogWarning($"User past exercise document not found. UserId: {userId} Method: {nameof(GetPastExerciseData)}");

                    var newDoc = await _helperService.CreateMissingDoc(userId, _exercisesHistoryCollection);
                    if(newDoc == null)
                    {
                        return (new BasicErrorResponse() { Success = false, ErrorCode = ErrorCodes.NotFound, ErrorMessage = "User exercise history document not found." }, null);
                    }

                    return (new BasicErrorResponse() { Success = true, ErrorCode = ErrorCodes.None, ErrorMessage = $"Correctly retrived {exerciseName} data but document is empty." }, new ExercisePastDataVMO() { ExerciseName = exerciseName});
                }

                var targetedExercise = userPastExercisesDoc.ExerciseHistoryLists.FirstOrDefault(a=>a.ExerciseName == exerciseName);
                if(targetedExercise == null)
                {
                    return (new BasicErrorResponse() { Success = true, ErrorCode = ErrorCodes.None, ErrorMessage = $"No past data for {exerciseName} found."}, new ExercisePastDataVMO() { ExerciseName = exerciseName});
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

                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.None, Success = true, ErrorMessage = "Sucessfully retrived data." }, exercisePastDataVMO);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get past data of an exercise. UserId: {userId} ExerciseName: {exerciseName} Method: {nameof(GetPastExerciseData)}");
                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"Error occured: {ex.Message}", Success = false }, null);
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

        public async Task<(BasicErrorResponse error, MuscleUsageDataVMO? data)> GetMuscleUsageData(string userId, string period = "all")
        {
            try
            {
                var userExercisesDataDoc = await _exercisesHistoryCollection.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if (userExercisesDataDoc == null)
                {
                    _logger.LogWarning($"user {userId} exercise history collection does not exist. creating.");
                    await _helperService.CreateMissingDoc(userId, _exercisesHistoryCollection);
                    return (new BasicErrorResponse() { ErrorCode = ErrorCodes.None, ErrorMessage = "Sucess", Success = true }, new MuscleUsageDataVMO());
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

                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.None, ErrorMessage = "Sucess", Success = true}, vmo);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get muscle usage data UserId: {userId} Period: {period} Method: {nameof(GetMuscleUsageData)}");
                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"Error occured: {ex.Message}", Success = false }, null);
            }
        }

        public async Task<(BasicErrorResponse error, MakroDataVMO? data)> GetPastMakroData(string userId, string period = "all")
        {
            try
            {
                var dietHistoryDocument = await _dietHistoryCollection.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if (dietHistoryDocument == null)
                {
                    _logger.LogWarning($"user {userId} diet history collection does not exist. creating...");
                    await _helperService.CreateMissingDoc(userId, _dietHistoryCollection);
                    return (new BasicErrorResponse() { ErrorCode = ErrorCodes.None, ErrorMessage = "Sucess", Success = true }, new MakroDataVMO());
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

                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.None, ErrorMessage = "Sucess", Success = true }, vmo);
            } 
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get past makro data. UserId: {userId} Period: {period} Method: {nameof(GetPastMakroData)}");
                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An error occured: {ex.Message}", Success = false }, null);
            }
        }

        public async Task<(BasicErrorResponse error, DailyMakroDistributionVMO? data)> GetDailyMakroDisturbtion(string userId, DateTime date)
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
                        return (new BasicErrorResponse() { ErrorCode = ErrorCodes.None, ErrorMessage = "Sucess", Success = true }, new DailyMakroDistributionVMO() { Date = date });
                    }

                    return (new BasicErrorResponse() { ErrorCode = ErrorCodes.NotFound, ErrorMessage = "User diet document not found.", Success = false }, null);
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

                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.None, ErrorMessage = "Sucess", Success = true }, vmo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get daily makro disturbtion for user. UserId: {userId} Datae: {date} Method: {nameof(GetDailyMakroDisturbtion)}");
                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An error occured: {ex.Message}", Success = true }, null);
            }
        }

        public async Task<BasicErrorResponse> UpdateLayout(string userId, UserLayoutVM model)
        {
            try
            {
                var user = await _dbContext.AppUser.FirstOrDefaultAsync(a => a.Id == userId);
                if (user == null)
                {
                    return new BasicErrorResponse() { ErrorCode = ErrorCodes.NotFound, ErrorMessage = "User not found.", Success = false };
                }

                LayoutSettings settings = new LayoutSettings()
                {
                    Animations = model.Animations,
                    ChartStack = model.ChartStack,
                };

                user.LayoutSettings = settings;

                _dbContext.AppUser.Update(user);
                await _dbContext.SaveChangesAsync();

                return new BasicErrorResponse() { ErrorCode = ErrorCodes.None, Success  = true, ErrorMessage = "Sucess" };
            } 
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to update user layout. UserId: {userId} Data: {model} Method: {nameof(UpdateLayout)}");
                return new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"Error occured: {ex}", Success = false };
            }
        }

    }
}
