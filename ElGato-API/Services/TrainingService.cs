using ElGato_API.Data;
using ElGato_API.Interfaces;
using ElGato_API.Models.Training;
using ElGato_API.ModelsMongo.History;
using ElGato_API.ModelsMongo.Training;
using ElGato_API.VM.Training;
using ElGato_API.VM.UserData;
using ElGato_API.VMO.ErrorResponse;
using ElGato_API.VMO.Training;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;

namespace ElGato_API.Services
{
    public class TrainingService : ITrainingService
    {
        private readonly AppDbContext _context;       
        private readonly IMongoCollection<DailyTrainingDocument> _trainingCollection;
        private readonly IMongoCollection<TrainingHistoryDocument> _trainingHistoryCollection;
        private readonly IMongoCollection<LikedExercisesDocument> _trainingLikesCollection;
        private readonly IMongoCollection<ExercisesHistoryDocument> _exercisesHistoryCollection;
        private readonly IMongoCollection<SavedTrainingsDocument> _savedTrainingsCollection;
        private readonly IHelperService _helperService;
        private readonly IUserService _userService;
        private readonly ILogger<TrainingService> _logger;
        public TrainingService(IMongoDatabase database, AppDbContext context, ILogger<TrainingService> logger, IHelperService helperService, IUserService userService) 
        {
            _trainingCollection = database.GetCollection<DailyTrainingDocument>("DailyTraining");
            _trainingHistoryCollection = database.GetCollection<TrainingHistoryDocument>("TrainingHistory");
            _trainingLikesCollection = database.GetCollection<LikedExercisesDocument>("LikedExercises");
            _exercisesHistoryCollection = database.GetCollection<ExercisesHistoryDocument>("ExercisesHistory");
            _savedTrainingsCollection = database.GetCollection<SavedTrainingsDocument>("SavedTrainings");
            _helperService = helperService;
            _userService = userService;
            _context = context;
            _logger = logger;
        }

        public async Task<(ErrorResponse error, List<ExerciseVMO>? data)> GetAllExercises()
        {
            try
            {
                var exercises = await _context.Exercises.Include(e => e.MusclesEngaded).ToListAsync();

                var response = exercises.Select(e => new ExerciseVMO
                {
                    Id = e.Id,
                    Name = e.Name,
                    Desc = e.Desc,
                    Image = e.Image,
                    ImgGifPart = e.ImageGifPart,
                    MusclesEngaged = e.MusclesEngaded.Select(m => new MuscleVMO
                    {
                        Id = m.Id,
                        Name = m.Name,
                        NormalName = m.NormalName,
                        Group = m.Group.ToString()
                    }).ToList(),
                    MainBodyPart = e.MainBodyPart.ToString(),
                    SpecificBodyPart = e.SpecificBodyPart.ToString(),
                    Equipment = e.Equipment.ToString(),
                    Difficulty = e.Difficulty.ToString()
                }).ToList();

                return (ErrorResponse.Ok(), response);
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, $"Failed to get all exercises. Method: {nameof(GetAllExercises)}");
                return (ErrorResponse.Internal(ex.Message), null);
            }
        }

        public async Task<(ErrorResponse error, List<LikedExercisesVMO>? data)> GetAllLikedExercises(string userId)
        {
            try
            {
                List<LikedExercisesVMO> likedExercises = new List<LikedExercisesVMO>();

                var userLikesDoc = await _trainingLikesCollection.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if (userLikesDoc == null)
                {
                    LikedExercisesDocument doc = new LikedExercisesDocument()
                    {
                        UserId = userId,
                        Own = new List<LikedOwn>(),
                        Premade = new List<LikedExercise>()
                    };

                    await _trainingLikesCollection.InsertOneAsync(doc);
                    return (ErrorResponse.Ok(), likedExercises);
                }

                foreach(var exercie in userLikesDoc.Own)
                {
                    likedExercises.Add(new LikedExercisesVMO() { Name = exercie.Name, Own = true, MuscleType = exercie.MuscleType });
                }

                foreach(var exercise in userLikesDoc.Premade)
                {
                    likedExercises.Add(new LikedExercisesVMO() { Name = exercise.Name, Id = exercise.Id, Own = false });
                }

                return (ErrorResponse.Ok(), likedExercises);
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, $"Failed to get all liked exercises. Method: {nameof(GetAllLikedExercises)}");
                return (ErrorResponse.Internal(ex.Message), null);
            }
        }

        public async Task<(ErrorResponse error, TrainingDayVMO? data)> GetUserTrainingDay(string userId, DateTime date)
        {
            try
            {
                var userTrainingDocument = await _trainingCollection.Find(a=>a.UserId == userId).FirstOrDefaultAsync();
                if(userTrainingDocument == null) 
                { 
                    _logger.LogWarning($"Training document not found. UserId: {userId} Method: {nameof(GetUserTrainingDay)}");

                    var newDoc = await _helperService.CreateMissingDoc(userId, _trainingCollection);
                    if(newDoc == null)
                    {
                        return (ErrorResponse.NotFound("User training document not found"), null);
                    }

                    userTrainingDocument = newDoc;
                }
             
                var targetedPlan = userTrainingDocument.Trainings.FirstOrDefault(a=>a.Date == date);
                if(targetedPlan != null)
                {
                    List<TrainingDayExerciseVMO> modelList = new List<TrainingDayExerciseVMO>();
                    foreach (var ex in targetedPlan.Exercises)
                    {
                        var pastData = await GetPastDataFromExercise(userId, date, ex.Name);

                        TrainingDayExerciseVMO modelExercise = new TrainingDayExerciseVMO()
                        {
                            Exercise = ex,
                            PastData = pastData,
                        };

                        modelList.Add(modelExercise);
                    }

                    TrainingDayVMO data = new TrainingDayVMO()
                    {
                        Date = date,
                        Exercises = modelList,
                    };

                    return (ErrorResponse.Ok(), data);
                }

                if(userTrainingDocument.Trainings != null && userTrainingDocument.Trainings.Count() >= 7)
                {
                    var oldestTraining = userTrainingDocument.Trainings.OrderBy(dp => dp.Date).First();
                    await MoveTrainingToHistory(userId, oldestTraining);

                    var update = Builders<DailyTrainingDocument>.Update.PullFilter(d => d.Trainings, dp => dp.Date == oldestTraining.Date);
                    await _trainingCollection.UpdateOneAsync(d => d.UserId == userId, update);
                }

                DailyTrainingPlan trainingUpd = new DailyTrainingPlan()
                {
                    Date = date,
                    Exercises = new List<DailyExercise>(),
                };

                var updated = Builders<DailyTrainingDocument>.Update.Push(d => d.Trainings, trainingUpd);
                await _trainingCollection.UpdateOneAsync(d => d.UserId == userId, updated);

                return (ErrorResponse.Ok(), new TrainingDayVMO() { Date = date, Exercises = new List<TrainingDayExerciseVMO>() });
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed to GetUserTrainingDay UserId: {userId} Date: {date} Method: {nameof(GetUserTrainingDay)}");
                return (ErrorResponse.Internal(ex.Message), null);
            }
        }

        public async Task<(ErrorResponse error, SavedTrainingsVMO? data)> GetSavedTrainings(string userId)
        {
            try
            {
                var userSavedTrainingDoc = await _savedTrainingsCollection.Find(a=>a.UserId == userId).FirstOrDefaultAsync();
                if (userSavedTrainingDoc == null)
                {
                    SavedTrainingsDocument newDoc = new SavedTrainingsDocument()
                    {
                        UserId = userId,
                        SavedTrainings = new List<SavedTrainings>()
                    };

                    await _savedTrainingsCollection.InsertOneAsync(newDoc);
                    SavedTrainingsVMO data = new SavedTrainingsVMO()
                    {
                        SavedTrainings = new List<SavedTrainings>(),
                    };
                    return (ErrorResponse.Ok(), data);
                }

                SavedTrainingsVMO vmoData = new SavedTrainingsVMO()
                {
                    SavedTrainings = userSavedTrainingDoc.SavedTrainings,
                };

                return (ErrorResponse.Ok(), vmoData);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get user saved trainings. UserId: {userId} Method: {nameof(GetSavedTrainings)}");
                return (ErrorResponse.Internal(ex.Message), null);
            }
        }

        public async Task<double> GetTotalExerciseWeightValue(string userId, DateTime date, List<int> exercisePublicIds, IClientSessionHandle session = null)
        {
            try
            {
                var userTrainingDoc = session != null ? await _trainingCollection.Find(session, d => d.UserId == userId).FirstOrDefaultAsync() : await _trainingCollection.Find(d => d.UserId == userId).FirstOrDefaultAsync();
                if(userTrainingDoc == null)
                {
                    return 0;
                }

                var targetPlan = userTrainingDoc.Trainings.FirstOrDefault(p => p.Date.Date == date.Date);

                if (targetPlan == null)
                {
                    return 0;
                }

                var matchingExercises = targetPlan.Exercises.Where(ex => exercisePublicIds.Contains(ex.PublicId));

                double totalWeight = 0.0;
                foreach (var exercise in matchingExercises)
                {
                    if (exercise.Series == null)
                        continue;

                    foreach (var series in exercise.Series)
                    {
                        totalWeight += series.WeightKg * series.Repetitions;
                    }
                }

                return totalWeight;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get total exercise weight. UserId: {userId} Date: {date} Ids: {exercisePublicIds}, Method: {nameof(GetTotalExerciseWeightValue)}");
                return 0;
            }
        }

        public async Task<List<int>> GetExerciseInTrainingDayPublicIds(string userId, DateTime date, IClientSessionHandle session = null)
        {
            try
            {
                var publicIdsList = new List<int>();

                var userTrainingDoc = session != null ? await _trainingCollection.Find(session, d => d.UserId == userId).FirstOrDefaultAsync() : await _trainingCollection.Find(d => d.UserId == userId).FirstOrDefaultAsync();
                if (userTrainingDoc == null)
                {
                    return publicIdsList;
                }

                var targetPlan = userTrainingDoc.Trainings.FirstOrDefault(p => p.Date.Date == date.Date);

                if (targetPlan == null)
                {
                    return publicIdsList;
                }            

                foreach(var ex in targetPlan.Exercises)
                {
                    publicIdsList.Add(ex.PublicId);
                }

                return publicIdsList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get total exercise weight. UserId: {userId} Date: {date} Ids: {GetExerciseInTrainingDayPublicIds}, Method: {nameof(GetTotalExerciseWeightValue)}");
                return new List<int>();
            }
        }
        public async Task<ErrorResponse> SaveTraining(string userId, SaveTrainingVM model)
        {
            try
            {
                var userSavedTrainingDoc = await _savedTrainingsCollection.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if (userSavedTrainingDoc == null)
                {
                    List<SavedExercises> exercises = new List<SavedExercises>();
                    int idCounter = 0;
                    foreach(var name in model.ExerciseNames)
                    {
                        exercises.Add(new SavedExercises() { Name = name, PublicId = idCounter});
                        idCounter++;
                    }

                    SavedTrainingsDocument newDoc = new SavedTrainingsDocument()
                    {
                        UserId = userId,
                        SavedTrainings = new List<SavedTrainings>() { new SavedTrainings() { Name = model.Name, Exercises = exercises, PublicId = 0 } }
                    };

                    await _savedTrainingsCollection.InsertOneAsync(newDoc);
                    return ErrorResponse.Ok();
                }

                int newId = (userSavedTrainingDoc.SavedTrainings.Any()) ? userSavedTrainingDoc.SavedTrainings.Max(a => a.PublicId) + 1 : 0;
                List<SavedExercises> exercisesList = new List<SavedExercises>();
                int counter = 0;

                foreach (var name in model.ExerciseNames)
                {
                    exercisesList.Add(new SavedExercises() { Name = name, PublicId = counter });
                    counter++;
                }

                userSavedTrainingDoc.SavedTrainings.Add(new SavedTrainings() { Name = model.Name, Exercises = exercisesList, PublicId = newId});
                var updateRes = await _savedTrainingsCollection.ReplaceOneAsync(a => a.UserId == userId, userSavedTrainingDoc);

                if (updateRes.ModifiedCount == 0)
                {
                    _logger.LogError($"Mogo update failed while trying to save training. UserId: {userId} Data: {model} Method: {nameof(SaveTraining)} ");
                    return ErrorResponse.Failed("Failed to update sacved training data");
                }

                return ErrorResponse.Ok();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to save training. UserId: {userId} Data: {model} Method: {nameof(SaveTraining)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<ErrorResponse> AddExercisesToTrainingDay(string userId, AddExerciseToTrainingVM model)
        {
            try
            {
                var userTrainingDocument = await _trainingCollection.Find(a=>a.UserId == userId).FirstOrDefaultAsync();
                if (userTrainingDocument == null) 
                { 
                    _logger.LogWarning($"User training document not found. UserId: {userId} Method: {nameof(AddExercisesToTrainingDay)}");

                    var newDoc = await _helperService.CreateMissingDoc(userId, _trainingCollection);
                    if(newDoc == null)
                    {
                        return ErrorResponse.NotFound("User daily training document not found, couldnt perform any action.");
                    }

                    userTrainingDocument = newDoc;
                }

                var targetedPlan = userTrainingDocument.Trainings.FirstOrDefault(a => a.Date == model.Date);
                if (targetedPlan == null)
                {
                    _logger.LogWarning($"Training day not found by date. UserId: {userId} Date: {model.Date} Method: {nameof(AddExercisesToTrainingDay)}");
                    return ErrorResponse.NotFound("couldn''y find any matching dates in training document, proces terminated");
                }

                int lastId = 0;
                if (targetedPlan.Exercises != null && targetedPlan.Exercises.Count() > 0)
                {
                    lastId = targetedPlan.Exercises[targetedPlan.Exercises.Count() - 1].PublicId + 1;
                }

                List<DailyExercise> listOfExercisesForInsertion = new List<DailyExercise>();

                foreach(var ex in model.Name)
                {
                    DailyExercise daileEx = new DailyExercise()
                    {
                        Name = ex,
                        PublicId = lastId,
                        Series = new List<ExerciseSeries>() { new ExerciseSeries() { PublicId = 1, Repetitions = 0, WeightKg = 0, WeightLbs = 0, Tempo = new ExerciseSerieTempo() { } } },
                    };

                    listOfExercisesForInsertion.Add(daileEx);
                    lastId++;
                }

                targetedPlan.Exercises.AddRange(listOfExercisesForInsertion);

                await _trainingCollection.ReplaceOneAsync(a=>a.UserId == userId, userTrainingDocument);

                return ErrorResponse.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while tring to add exercise to training day. UserId: {userId} Data: {model} Method: {nameof(AddExercisesToTrainingDay)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<ErrorResponse> LikeExercise(string userId, LikeExerciseVM model, IClientSessionHandle session = null)
        {
            try
            {
                LikedExercisesDocument existingDoc = session != null ?
                    await _trainingLikesCollection.Find(session, a => a.UserId == userId).FirstOrDefaultAsync() :
                    await _trainingLikesCollection.Find(a => a.UserId == userId).FirstOrDefaultAsync();

                if (existingDoc == null)
                {
                    var doc = new LikedExercisesDocument()
                    {
                        UserId = userId,
                    };

                    if (model.Own)
                    {
                        doc.Own = new List<LikedOwn>()
                        {
                            new LikedOwn() { Name = model.Name, MuscleType = model.MuscleType }
                        };
                        doc.Premade = new List<LikedExercise>();
                    }
                    else
                    {
                        var existingEx = await _context.Exercises.FirstOrDefaultAsync(a => a.Id == model.Id);
                        if (existingEx == null)
                        {
                            return ErrorResponse.NotFound("Given premade exercise not found");
                        }

                        doc.Own = new List<LikedOwn>();
                        doc.Premade = new List<LikedExercise>()
                        {
                            new LikedExercise() { Name = model.Name, Id = model.Id ?? existingEx.Id }
                        };
                    }

                    if (session != null)
                        await _trainingLikesCollection.InsertOneAsync(session, doc);
                    else
                        await _trainingLikesCollection.InsertOneAsync(doc);

                    return ErrorResponse.Ok();
                }

                if (model.Own)
                {
                    var alreadyExist = existingDoc.Own.FirstOrDefault(a => a.Name == model.Name);
                    if (alreadyExist != null)
                    {
                        return ErrorResponse.AlreadyExists("Own exercise with given name already saved");
                    }
                    existingDoc.Own.Add(new LikedOwn() { Name = model.Name, MuscleType = model.MuscleType });
                }
                else
                {
                    var existingEx = await _context.Exercises.FirstOrDefaultAsync(a => a.Id == model.Id);
                    if (existingEx == null)
                    {
                        return ErrorResponse.NotFound("Given premade exercise not found");
                    }
                    var alreadyExists = existingDoc.Premade.FirstOrDefault(a => a.Id == model.Id);
                    if (alreadyExists != null)
                    {
                        return ErrorResponse.AlreadyExists("Premade exercise with given name already saved");
                    }
                    existingDoc.Premade.Add(new LikedExercise() { Name = model.Name, Id = model.Id ?? existingEx.Id });
                }

                if (session != null)
                    await _trainingLikesCollection.ReplaceOneAsync(session, a => a.UserId == userId, existingDoc);
                else
                    await _trainingLikesCollection.ReplaceOneAsync(a => a.UserId == userId, existingDoc);

                return ErrorResponse.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to LikeExercise. UserId: {userId} Data: {model} Method: {nameof(LikeExercise)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }



        public async Task<ErrorResponse> RemoveExercisesFromLiked(string userId, List<LikeExerciseVM> model)
        {
            try 
            {
                var existingDoc = await _trainingLikesCollection.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if (existingDoc == null) 
                { 
                    _logger.LogWarning($"Liked training document not found. UserId: {userId} Method: {nameof(RemoveExercisesFromLiked)}");

                    var newDoc = await _helperService.CreateMissingDoc(userId, _trainingLikesCollection);
                    if(newDoc == null)
                    {
                        return ErrorResponse.NotFound("User liked exercise document not found.");
                    }

                    existingDoc = newDoc;
                }

                foreach (var exercise in model) 
                {
                    if (exercise.Own)
                    {
                        var exerciseToRemove = existingDoc.Own.FirstOrDefault(a => a.Name == exercise.Name);
                        if (exerciseToRemove == null)
                        {
                            return ErrorResponse.NotFound($"Given own exercise not found {exercise.Name}");
                        }

                        existingDoc.Own.Remove(exerciseToRemove);
                    }
                    else
                    {
                        var exerciseToRemove = existingDoc.Premade.FirstOrDefault(a => a.Id == exercise.Id);
                        if (exerciseToRemove == null)
                        {
                            return ErrorResponse.NotFound($"Given premade exercise not found {exercise.Name},{exercise.Id}");
                        }

                        existingDoc.Premade.Remove(exerciseToRemove);
                    }
                }

                await _trainingLikesCollection.ReplaceOneAsync(a => a.UserId == userId, existingDoc);
                return ErrorResponse.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to remove exercise from liked. UserId: {userId} Data: {model} Method: {nameof(RemoveExercisesFromLiked)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }


        private async Task MoveTrainingToHistory(string userId, DailyTrainingPlan oldestPlan)
        {
            var trainingHistoryDoc = await _trainingHistoryCollection.Find(a=>a.UserId == userId).FirstOrDefaultAsync();
            if (trainingHistoryDoc == null)
            {
                TrainingHistoryDocument historyDoc = new TrainingHistoryDocument()
                {
                    UserId = userId,
                    DailyTrainingPlans = new List<DailyTrainingPlan> { oldestPlan }
                };

                await _trainingHistoryCollection.InsertOneAsync(historyDoc);
                return;
            }

            var update = Builders<TrainingHistoryDocument>.Update.Push(h => h.DailyTrainingPlans, oldestPlan);
            await _trainingHistoryCollection.UpdateOneAsync(h => h.UserId == userId, update);
        }

        private async Task<PastExerciseData?> GetPastDataFromExercise(string userId, DateTime currentDate, string exerciseName)
        {
            PastExerciseData data = null;

            var filter = Builders<ExercisesHistoryDocument>.Filter.And(
                Builders<ExercisesHistoryDocument>.Filter.Eq(e => e.UserId, userId),
                Builders<ExercisesHistoryDocument>.Filter.ElemMatch(
                    e => e.ExerciseHistoryLists,
                    eh => eh.ExerciseName == exerciseName
                )
            );

            var projection = Builders<ExercisesHistoryDocument>.Projection.Expression(doc =>
                doc.ExerciseHistoryLists
                    .Where(eh => eh.ExerciseName == exerciseName)
                    .SelectMany(eh => eh.ExerciseData)
                    .Where(ed => ed.Date < currentDate)
                    .OrderByDescending(ed => ed.Date)
                    .FirstOrDefault()
            );

            var res = await _exercisesHistoryCollection
                .Find(filter)
                .Project(projection)
                .FirstOrDefaultAsync();

            if(res != null)
            {
                data = new PastExerciseData();
                data.Series = res.Series??new List<ExerciseSeries>();
                data.Date = res.Date;
            }


            return data;
        }

        public async Task<ErrorResponse> WriteSeriesForAnExercise(string userId, AddSeriesToAnExerciseVM model, IClientSessionHandle session = null)
        {
            try
            {
                if (!model.Series.Any())
                {
                    model.Series.Add(new AddSeriesVM()
                    {
                        Repetitions = 0,
                        WeightKg = 0,
                        WeightLbs = 0,
                    });
                }

                foreach (var series in model.Series)
                {
                    if (series.WeightKg == 0)
                    {
                        series.WeightKg = (series.WeightLbs / 2.20462);
                    }
                    else if (series.WeightLbs == 0)
                    {
                        series.WeightLbs = (series.WeightKg * 2.20462);
                    }
                }

                var filter = Builders<DailyTrainingDocument>.Filter.And(
                    Builders<DailyTrainingDocument>.Filter.Eq(t => t.UserId, userId),
                    Builders<DailyTrainingDocument>.Filter.ElemMatch(t => t.Trainings, training => training.Date == model.Date)
                );

                var trainingDocument = session != null ? await _trainingCollection.Find(session, filter).FirstOrDefaultAsync() : await _trainingCollection.Find(filter).FirstOrDefaultAsync();
                if (trainingDocument == null)
                {
                    _logger.LogWarning($"Training document not found. UserId: {userId} Method: {nameof(WriteSeriesForAnExercise)}");

                    var newDoc = await _helperService.CreateMissingDoc(userId, _trainingCollection);
                    if(newDoc == null)
                    {
                        return ErrorResponse.NotFound("Training document not found for the given user");
                    }

                    trainingDocument = newDoc;
                }

                var trainingIndex = trainingDocument.Trainings.FindIndex(t => t.Date == model.Date);
                if (trainingIndex == -1)
                {
                    _logger.LogWarning($"Training document session not found for given date. UserId: {userId} Date: {model.Date} Method: {nameof(WriteSeriesForAnExercise)}");
                    return ErrorResponse.NotFound("Training session not found for the given date");
                }

                var exerciseIndex = trainingDocument.Trainings[trainingIndex].Exercises.FindIndex(e => e.PublicId == model.PublicId);
                if (exerciseIndex == -1)
                {
                    return ErrorResponse.NotFound("Exercise not found in the training session");
                }

                int highestPublicId = trainingDocument.Trainings[trainingIndex].Exercises[exerciseIndex].Series.Any()
                    ? trainingDocument.Trainings[trainingIndex].Exercises[exerciseIndex].Series.Max(s => s.PublicId)
                    : 0;

                var newSeriesList = model.Series.Select((series, index) => new ExerciseSeries
                {
                    PublicId = highestPublicId + index + 1,
                    Repetitions = series.Repetitions,
                    WeightKg = series.WeightKg,
                    WeightLbs = series.WeightLbs
                }).ToList();

                var updateFilter = Builders<DailyTrainingDocument>.Filter.And(
                    Builders<DailyTrainingDocument>.Filter.Eq(t => t.UserId, userId),
                    Builders<DailyTrainingDocument>.Filter.Eq($"Trainings.{trainingIndex}.Exercises.{exerciseIndex}.PublicId", model.PublicId)
                );

                var update = Builders<DailyTrainingDocument>.Update.PushEach($"Trainings.{trainingIndex}.Exercises.{exerciseIndex}.Series", newSeriesList);

                var result = session != null ? await _trainingCollection.UpdateOneAsync(session, updateFilter, update) : await _trainingCollection.UpdateOneAsync(updateFilter, update);

                if (result.ModifiedCount == 0)
                {
                    _logger.LogError($"Mongo update failed while trying to add exercises to the session by date. UserId: {userId} Data: {model} Method: {nameof(WriteSeriesForAnExercise)}");
                    return ErrorResponse.Failed();
                }

                return ErrorResponse.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while tring to write series to an exercise. UserId: {userId} Data: {model} Method: {nameof(WriteSeriesForAnExercise)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<ErrorResponse> AddSavedTrainingToTrainingDay(string userId, AddSavedTrainingToTrainingDayVM model)
        {
            try
            {
                var filter = Builders<SavedTrainingsDocument>.Filter.Eq(doc => doc.UserId, userId) &
                     Builders<SavedTrainingsDocument>.Filter.ElemMatch(doc => doc.SavedTrainings, st => st.PublicId == model.SavedTrainingId);

                var userSavedTrainings = await _savedTrainingsCollection.Find(filter).FirstOrDefaultAsync();
                if (userSavedTrainings == null)
                {
                    _logger.LogWarning($"Saved training document not found. UserId: {userId} Method: {nameof(AddSavedTrainingToTrainingDay)}");

                    var newDoc = await _helperService.CreateMissingDoc(userId, _savedTrainingsCollection);
                    if(newDoc == null)
                    {
                        return ErrorResponse.NotFound($"Couldnt find training with given id for the user.");
                    }

                    userSavedTrainings = newDoc;
                }

                var targetTraining = userSavedTrainings.SavedTrainings.FirstOrDefault(a => a.PublicId == model.SavedTrainingId);
                if (targetTraining == null) { return ErrorResponse.NotFound($"user does not have saved training with given id {model.SavedTrainingId}"); }

                var userTrainingDocument = await _trainingCollection.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if (userTrainingDocument == null) { return ErrorResponse.NotFound("User daily training document not found, couldnt perform any action."); }

                var targetedPlan = userTrainingDocument.Trainings.FirstOrDefault(a => a.Date == model.Date);
                if (targetedPlan == null) { return ErrorResponse.NotFound("couldn''y find any matching dates in training document, proces terminated"); }

                int lastPublicId = 0;
                if (targetedPlan.Exercises.Any())
                {
                    lastPublicId = targetedPlan.Exercises.Max(a => a.PublicId) + 1;
                }

                foreach (var ex in targetTraining.Exercises)
                {
                    DailyExercise newRecord = new DailyExercise()
                    {
                        Name = ex.Name,
                        IsLiked = false,
                        PublicId = lastPublicId,
                        Series = new List<ExerciseSeries>() { new ExerciseSeries() { PublicId = 1, Repetitions = 0, WeightKg = 0, WeightLbs = 0 } }
                    };

                    targetedPlan.Exercises.Add(newRecord);
                    lastPublicId++;
                }


                await _trainingCollection.ReplaceOneAsync(a => a.UserId == userId, userTrainingDocument);
                return ErrorResponse.Ok();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to add saved training to training day. UserId: {userId} Data: {model} Method: {nameof(AddSavedTrainingToTrainingDay)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<ErrorResponse> UpdateExerciseHistory(string userId, HistoryUpdateVM model, DateTime date, IClientSessionHandle session = null)
        {
            try
            {
                ExercisesHistoryDocument userHistoryDocument;
                if(session == null)
                {
                    userHistoryDocument = await _exercisesHistoryCollection.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                }
                else
                {
                    userHistoryDocument = await _exercisesHistoryCollection.Find(session, a => a.UserId == userId).FirstOrDefaultAsync();
                }

                if (userHistoryDocument == null)
                {
                    ExercisesHistoryDocument doc = new ExercisesHistoryDocument()
                    {
                        UserId = userId,
                        ExerciseHistoryLists = new List<ExerciseHistoryList>()
                        {
                            new ExerciseHistoryList()
                            {
                                ExerciseName = model.ExerciseName,
                                MuscleType = await GetMuscleType(model.ExerciseName, userId),
                                ExerciseData = new List<ExerciseData>()
                                {
                                    model.ExerciseData,
                                }
                            }
                            
                        }
                    };

                    if(session == null)
                    {
                        await _exercisesHistoryCollection.InsertOneAsync(doc);
                    }
                    else
                    {
                        await _exercisesHistoryCollection.InsertOneAsync(session, doc);
                    }

                    return ErrorResponse.Ok();
                }

                var givenExercisePastData = userHistoryDocument.ExerciseHistoryLists.FirstOrDefault(a=>a.ExerciseName == model.ExerciseName);
                if(givenExercisePastData == null)
                {
                    ExerciseHistoryList newRecord = new ExerciseHistoryList()
                    {
                        ExerciseName = model.ExerciseName,
                        MuscleType = await GetMuscleType(model.ExerciseName, userId),
                        ExerciseData = new List<ExerciseData>() { model.ExerciseData }
                    };

                    userHistoryDocument.ExerciseHistoryLists.Add(newRecord);

                    var filter = Builders<ExercisesHistoryDocument>.Filter.Eq(a => a.UserId, userId);
                    var update = Builders<ExercisesHistoryDocument>.Update.Set(a => a.ExerciseHistoryLists, userHistoryDocument.ExerciseHistoryLists);

                    if(session == null)
                    {
                        await _exercisesHistoryCollection.UpdateOneAsync(filter, update);
                    }
                    else
                    {
                        await _exercisesHistoryCollection.UpdateOneAsync(session, filter, update);
                    }

                    return ErrorResponse.Ok();
                }

                var exercisePastDay = givenExercisePastData.ExerciseData.FirstOrDefault(a => a.Date == date);
                if (exercisePastDay == null)
                {
                    givenExercisePastData.ExerciseData.Add(model.ExerciseData);
                }
                else
                {
                    exercisePastDay.Series = model.ExerciseData.Series;
                }

                var updateFilter = Builders<ExercisesHistoryDocument>.Filter.Eq(a => a.UserId, userId);
                var updateDefinition = Builders<ExercisesHistoryDocument>.Update.Set(a => a.ExerciseHistoryLists, userHistoryDocument.ExerciseHistoryLists);

                if(session == null)
                {
                    await _exercisesHistoryCollection.UpdateOneAsync(updateFilter, updateDefinition);
                }
                else
                {
                    await _exercisesHistoryCollection.UpdateOneAsync(session, updateFilter, updateDefinition);
                }

                return ErrorResponse.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to UpdateExerciseHistory. UserId: {userId} Data: {model} Date: {date} Method: {nameof(UpdateExerciseHistory)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<ErrorResponse> RemoveSeriesFromAnExercise(string userId, RemoveSeriesFromExerciseVM model, IClientSessionHandle session = null)
        {
            try
            {
                var trainingDocument = session != null ? await _trainingCollection.Find(session, a => a.UserId == userId).FirstOrDefaultAsync() : await _trainingCollection.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if (trainingDocument == null) 
                {
                    _logger.LogWarning($"Training document not found. UserId: {userId} Method: {nameof(RemoveSeriesFromAnExercise)}");

                    var newDoc = await _helperService.CreateMissingDoc(userId, _trainingCollection);
                    if(newDoc == null)
                    {
                        return ErrorResponse.NotFound("user training document not found.");
                    }

                    trainingDocument = newDoc;
                }

                if (trainingDocument.Trainings == null) { return ErrorResponse.NotFound("Could not remove any - daily training doc empty."); }

                var targetedDay = trainingDocument.Trainings.FirstOrDefault(a => a.Date == model.Date);
                if (targetedDay == null) { return ErrorResponse.NotFound("Current exercise day does not exist."); }

                var targetExercise = targetedDay.Exercises.FirstOrDefault(a => a.PublicId == model.ExercisePublicId);
                if (targetExercise == null) { return ErrorResponse.NotFound("Exercise not found. Couldnt perform remove operation."); }

                foreach (var idToRemove in model.seriesIdToRemove)
                {
                    var serieToRemove = targetExercise.Series.FirstOrDefault(a => a.PublicId == idToRemove);
                    if (serieToRemove != null)
                    {
                        targetExercise.Series.Remove(serieToRemove);
                    }
                }

                var updateResult = session != null ? await _trainingCollection.ReplaceOneAsync(session, doc => doc.UserId == userId, trainingDocument) : await _trainingCollection.ReplaceOneAsync(doc => doc.UserId == userId, trainingDocument);

                if (!updateResult.IsAcknowledged || updateResult.ModifiedCount == 0)
                {
                    _logger.LogError($"Mongo update failed while trying to RemoveSeriesFromAnExercise. UserId: {userId} Data: {model} Method: {nameof(RemoveSeriesFromAnExercise)}");
                    return ErrorResponse.Failed();
                }

                return ErrorResponse.Ok();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to remove series from an exercise. UserId: {userId} Data: {model} Method: {nameof(RemoveSeriesFromAnExercise)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<ErrorResponse> RemoveExerciseFromTrainingDay(string userId, RemoveExerciseFromTrainingDayVM model, IClientSessionHandle session = null)
        {
            try
            {
                var trainingDocument = session != null ? await _trainingCollection.Find(session, a => a.UserId == userId).FirstOrDefaultAsync() : await _trainingCollection.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if (trainingDocument == null) 
                {
                    _logger.LogWarning($"Training document not found. UserId: {userId} Method: {nameof(RemoveExerciseFromTrainingDay)}");

                    var newDoc = await _helperService.CreateMissingDoc(userId, _trainingCollection);
                    if(newDoc == null)
                    {
                        return ErrorResponse.NotFound("user training document not found.");
                    }

                    trainingDocument = newDoc;
                }

                if (trainingDocument.Trainings == null) { return ErrorResponse.Failed("Could not remove any - daily training doc empty."); }

                var targetedDay = trainingDocument.Trainings.FirstOrDefault(a => a.Date == model.Date);
                if (targetedDay == null) { return ErrorResponse.NotFound("Current exercise day does not exist."); }

                var targetExercise = targetedDay.Exercises.FirstOrDefault(a => a.PublicId == model.ExerciseId);
                if (targetExercise == null) { return ErrorResponse.NotFound("Exercise not found. Couldnt perform remove operation."); }

                targetedDay.Exercises.Remove(targetExercise);

                var updateResult = session != null ? await _trainingCollection.ReplaceOneAsync(session, doc => doc.UserId == userId, trainingDocument) : await _trainingCollection.ReplaceOneAsync(doc => doc.UserId == userId, trainingDocument);
                if (!updateResult.IsAcknowledged || updateResult.ModifiedCount == 0)
                {
                    _logger.LogError($"Mongo update failed while trying to remove exercises from training day. UserId: {userId} Data: {model} Method: {nameof(RemoveExerciseFromTrainingDay)}");
                    return ErrorResponse.Failed();
                }

                var similar = targetedDay.Exercises.Where(a=>a.Name == targetExercise.Name).ToList();

                List<ExerciseSeries> hisSeries = new List<ExerciseSeries>();

                if (similar.Any())
                {                   
                    foreach(var ex in similar)
                    {
                        hisSeries.AddRange(ex.Series);
                    }
                }

                HistoryUpdateVM hisModel = new HistoryUpdateVM()
                {
                    ExerciseName = targetExercise.Name,
                    ExerciseData = new ExerciseData() { Date = model.Date, Series = hisSeries }
                };

                var res = session != null ? await UpdateExerciseHistory(userId, hisModel, model.Date, session) : await UpdateExerciseHistory(userId, hisModel, model.Date);
                return res;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while tring to remove exercises from training day. UserId: {userId} Data: {model} Method: {nameof(RemoveExerciseFromTrainingDay)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<ErrorResponse> UpdateExerciseLikedStatus(string userId, string exerciseName, MuscleType type)
        {
            try
            {
                bool isExercisePremade = false;
                var premadeRec = await _context.Exercises.FirstOrDefaultAsync(a => a.Name == exerciseName);
                if (premadeRec != null) { isExercisePremade = true; }

                var userLikedDocument = await _trainingLikesCollection.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if (userLikedDocument == null)
                {
                    LikedExercisesDocument newDoc = new LikedExercisesDocument()
                    {
                        UserId = userId,
                        Own = new List<LikedOwn> { },
                        Premade = new List<LikedExercise>(),
                    };

                    if (isExercisePremade)
                    {
                        LikedExercise newRec = new LikedExercise()
                        {
                            Name = exerciseName,
                            Id = premadeRec.Id,
                        };

                        newDoc.Premade.Add(newRec);
                    }
                    else
                    {
                        newDoc.Own.Add(new LikedOwn() { Name = exerciseName, MuscleType = type });
                    }

                    await _trainingLikesCollection.InsertOneAsync(newDoc);
                    return ErrorResponse.Ok();
                }

                if (isExercisePremade)
                {
                    var alreadyExisting = userLikedDocument.Premade.FirstOrDefault(a => a.Name == exerciseName);
                    if (alreadyExisting != null)
                    {
                        userLikedDocument.Premade.Remove(alreadyExisting);
                    }
                    else
                    {
                        LikedExercise newRec = new LikedExercise()
                        {
                            Name = exerciseName,
                            Id = premadeRec.Id,
                        };
                        userLikedDocument.Premade.Add(newRec);
                    }
                }
                else
                {
                    var alreadyExisting = userLikedDocument.Own.FirstOrDefault(x => x.Name == exerciseName);
                    if (alreadyExisting != null)
                    {
                        userLikedDocument.Own.Remove(alreadyExisting);
                    }
                    else
                    {
                        userLikedDocument.Own.Add(new LikedOwn() { Name = exerciseName, MuscleType = type});
                    }
                }

                var filter = Builders<LikedExercisesDocument>.Filter.Eq(x => x.UserId, userId);
                await _trainingLikesCollection.ReplaceOneAsync(filter, userLikedDocument);

                return ErrorResponse.Ok();

            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to update like status. UserId: {userId} ExerciseName: {exerciseName} Method: {nameof(UpdateExerciseLikedStatus)} ");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<ErrorResponse> UpdateExerciseSeries(string userId, UpdateExerciseSeriesVM model, IClientSessionHandle session = null)
        {
            try
            {
                var userDailyTrainingDoc = session != null ? await _trainingCollection.Find(session, a => a.UserId == userId).FirstOrDefaultAsync() : await _trainingCollection.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if (userDailyTrainingDoc == null) 
                {
                    _logger.LogWarning($"Training document not found. UserId: {userId} Method: {nameof(UpdateExerciseSeries)}");

                    var newDoc = await _helperService.CreateMissingDoc(userId, _trainingCollection);
                    if(newDoc == null)
                    {
                        return ErrorResponse.NotFound("User daily training doc not found. couldnt perform update action.");
                    }

                    userDailyTrainingDoc = newDoc;
                }

                var targetedDay = userDailyTrainingDoc.Trainings.FirstOrDefault(a => a.Date == model.Date);
                if (targetedDay == null) { return ErrorResponse.NotFound("Couldnt find training day for a user for given date."); }

                var targetedExercise = targetedDay.Exercises.FirstOrDefault(a => a.PublicId == model.ExercisePublicId);
                if (targetedExercise == null) { return ErrorResponse.NotFound("Couldnt find any exercises with given publicId to perform update on."); }

                foreach (var serieToUpdate in model.SeriesToUpdate)
                {
                    var targetedSerie = targetedExercise.Series.FirstOrDefault(a => a.PublicId == serieToUpdate.SerieId);
                    if (targetedSerie != null)
                    {
                        targetedSerie.Repetitions = serieToUpdate.NewRepetitions;
                        targetedSerie.Tempo = serieToUpdate.newTempo;

                        if (serieToUpdate.NewWeightLbs == 0)
                        {
                            targetedSerie.WeightKg = serieToUpdate.NewWeightKg;
                            targetedSerie.WeightLbs = (serieToUpdate.NewWeightKg * 2.20462);
                        }
                        else
                        {
                            targetedSerie.WeightKg = (serieToUpdate.NewWeightLbs / 2.20462);
                            targetedSerie.WeightLbs = serieToUpdate.NewWeightLbs;
                        }
                    }
                }

                var write = session != null ? await _trainingCollection.ReplaceOneAsync(session, doc => doc.Id == userDailyTrainingDoc.Id, userDailyTrainingDoc) : await _trainingCollection.ReplaceOneAsync(doc => doc.Id == userDailyTrainingDoc.Id, userDailyTrainingDoc);
                return ErrorResponse.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to update exercise series. UserId: {userId} Data: {model} Method: {nameof(UpdateExerciseSeries)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<ErrorResponse> UpdateSavedTrainingName(string userId, UpdateSavedTrainingName model)
        {
            try
            {
                var userSavedTrainingsDoc = await _savedTrainingsCollection.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if(userSavedTrainingsDoc == null) 
                {
                    _logger.LogWarning($"Saved trainings document not found. UserId: {userId} Method: {nameof(UpdateSavedTrainingName)}");

                    var newDoc = await _helperService.CreateMissingDoc(userId, _savedTrainingsCollection);
                    if(newDoc == null)
                    {
                        return ErrorResponse.NotFound("document not found.");
                    }

                    userSavedTrainingsDoc = newDoc;
                }
           
                var targetedTraining = userSavedTrainingsDoc.SavedTrainings.FirstOrDefault(a=>a.PublicId == model.PublicId);
                if(targetedTraining == null) { return ErrorResponse.NotFound($"Training with given publicId {model.PublicId} does not exist"); }

                targetedTraining.Name = model.NewName;
                await _savedTrainingsCollection.ReplaceOneAsync(doc => doc.Id == userSavedTrainingsDoc.Id, userSavedTrainingsDoc);
                return ErrorResponse.Ok();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to update name of saved training. UserId: {userId} Data: {model} Method: {nameof(UpdateSavedTrainingName)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<ErrorResponse> RemoveTrainingsFromSaved(string userId, RemoveSavedTrainingsVM model)
        {
            try
            {
                var userDocument = await _savedTrainingsCollection.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if (userDocument == null)
                {
                    _logger.LogWarning($"Saved training collection not found. UserId: {userId} Method: {nameof(RemoveTrainingsFromSaved)}");

                    var newDoc = await _helperService.CreateMissingDoc(userId, _savedTrainingsCollection);
                    if(newDoc == null)
                    {
                        return ErrorResponse.NotFound("User saved training document not found.");
                    }

                    userDocument = newDoc;
                }

                foreach (var toRemoveId in model.SavedTrainingIdsToRemove)
                {
                    var target = userDocument.SavedTrainings.FirstOrDefault(a=>a.PublicId == toRemoveId);
                    if(target != null)
                    {
                        userDocument.SavedTrainings.Remove(target);
                    }
                }

                await _savedTrainingsCollection.ReplaceOneAsync(doc => doc.Id == userDocument.Id, userDocument);
                return ErrorResponse.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to remove training from saved trainings. UserId: {userId} Data: {model} Method: {nameof(RemoveTrainingsFromSaved)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<ErrorResponse> RemoveExercisesFromSavedTraining(string userId, DeleteExercisesFromSavedTrainingVM model)
        {
            try
            {
                var filter = Builders<SavedTrainingsDocument>.Filter.And(
                    Builders<SavedTrainingsDocument>.Filter.Eq(doc => doc.UserId, userId),
                    Builders<SavedTrainingsDocument>.Filter.ElemMatch(
                        doc => doc.SavedTrainings, t => t.PublicId == model.SavedTrainingPublicId)
                );

                var arrayFilter = new UpdateDefinitionBuilder<SavedTrainingsDocument>()
                    .PullFilter("SavedTrainings.$.Exercises", Builders<SavedExercises>.Filter.In(e => e.PublicId, model.ExercisesPublicIdToRemove));

                var result = await _savedTrainingsCollection.UpdateOneAsync(filter, arrayFilter);

                if (result.MatchedCount == 0)
                {
                    return ErrorResponse.NotFound($"Saved training with publicId {model.SavedTrainingPublicId} not found");
                }

                _logger.LogError($"Mongo update failed while trying to remove exercises from saved training. UserId: {userId} Data: {model} Method: {nameof(RemoveExercisesFromSavedTraining)}");
                return ErrorResponse.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed while trying to RemoveExercisesFromSavedTraining. UserId: {userId} Data: {model} Method: {nameof(RemoveExercisesFromSavedTraining)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }
       

        private async Task<MuscleType> GetMuscleType(string exerciseName, string userId)
        {
            try
            {
                var exercise = await _context.Exercises.FirstOrDefaultAsync(a => a.Name == exerciseName);
                if (exercise != null)
                {
                    return exercise.SpecificBodyPart switch
                    {
                        SpecificBodyPart.Chest or
                        SpecificBodyPart.UpperChest or
                        SpecificBodyPart.LowerChest => MuscleType.Chest,

                        SpecificBodyPart.Back or
                        SpecificBodyPart.Lats or
                        SpecificBodyPart.Traps => MuscleType.Back,

                        SpecificBodyPart.Shoulders => MuscleType.Shoulders,

                        SpecificBodyPart.Biceps or
                        SpecificBodyPart.Forearms or
                        SpecificBodyPart.Triceps => MuscleType.Arms,

                        SpecificBodyPart.Quads or
                        SpecificBodyPart.Calves or
                        SpecificBodyPart.Hamstrings or
                        SpecificBodyPart.Glutes => MuscleType.Legs,

                        SpecificBodyPart.Obliques or
                        SpecificBodyPart.Abs => MuscleType.Core,

                        _ => MuscleType.Unknown
                    };
                }

                var likedExercisesDoc = await _trainingLikesCollection.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if (likedExercisesDoc != null)
                {
                    var targetExercise = likedExercisesDoc.Own.FirstOrDefault(a => a.Name == exerciseName);
                    if (targetExercise != null)
                    {
                        return targetExercise.MuscleType;
                    }
                }

                return MuscleType.Unknown;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Issue while trying to get exercise muscle. Method: {nameof(GetMuscleType)}");
                return MuscleType.Unknown;
            }
        }

        public async Task<ErrorResponse> AddPersonalExerciseRecordToHistory(string userId, string exerciseName, MuscleType type, IClientSessionHandle session = null)
        {
            try
            {
                var userExerciseHistoryDocument = session != null ?
                    await _exercisesHistoryCollection.Find(session, a => a.UserId == userId).FirstOrDefaultAsync() :
                    await _exercisesHistoryCollection.Find(a => a.UserId == userId).FirstOrDefaultAsync();

                if (userExerciseHistoryDocument == null)
                {
                    _logger.LogWarning($"Saved training collection not found. UserId: {userId} Method: {nameof(AddPersonalExerciseRecordToHistory)}");

                    var newDoc = await _helperService.CreateMissingDoc(userId, _exercisesHistoryCollection);

                    if (newDoc == null)
                    {
                        return ErrorResponse.NotFound("User saved training document not found.");
                    }

                    userExerciseHistoryDocument = newDoc;
                }

                var targetExercise = userExerciseHistoryDocument.ExerciseHistoryLists
                    .FirstOrDefault(a => a.ExerciseName == exerciseName);

                if (targetExercise == null)
                {
                    var newExercise = new ExerciseHistoryList()
                    {
                        ExerciseName = exerciseName,
                        ExerciseData = new List<ExerciseData>(),
                        MuscleType = type
                    };

                    userExerciseHistoryDocument.ExerciseHistoryLists.Add(newExercise);

                    var addResult = session != null ?
                        await _exercisesHistoryCollection.ReplaceOneAsync(session, doc => doc.UserId == userId, userExerciseHistoryDocument) :
                        await _exercisesHistoryCollection.ReplaceOneAsync(doc => doc.UserId == userId, userExerciseHistoryDocument);

                    if (!addResult.IsAcknowledged)
                    {
                        return ErrorResponse.Failed();
                    }

                    return ErrorResponse.Ok();
                }

                if (targetExercise.MuscleType != type)
                {
                    var premadeAlreadyNamedThat = await _context.Exercises.FirstOrDefaultAsync(a => a.Name == exerciseName);
                    if (premadeAlreadyNamedThat != null)
                    {
                        return ErrorResponse.Ok();
                    }

                    targetExercise.MuscleType = type;

                    var updateResult = session != null ?
                        await _exercisesHistoryCollection.ReplaceOneAsync(session, doc => doc.UserId == userId, userExerciseHistoryDocument) :
                        await _exercisesHistoryCollection.ReplaceOneAsync(doc => doc.UserId == userId, userExerciseHistoryDocument);

                    if (!updateResult.IsAcknowledged)
                    {
                        return ErrorResponse.Failed();
                    }
                }

                return ErrorResponse.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while trying to add new personal exercise to exercise history. UserId: {userId} ExerciseName: {exerciseName} Method: {nameof(AddPersonalExerciseRecordToHistory)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }
    }
}
