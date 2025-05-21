using ElGato_API.Data;
using ElGato_API.Interfaces;
using ElGato_API.Models.Training;
using ElGato_API.Models.User;
using ElGato_API.ModelsMongo.Cardio;
using ElGato_API.ModelsMongo.History;
using ElGato_API.ModelsMongo.Training;
using ElGato_API.VM.Cardio;
using ElGato_API.VMO.Cardio;
using ElGato_API.VMO.ErrorResponse;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using System.Net.Sockets;
using System.Text;

namespace ElGato_API.Services
{
    public class CardioService : ICardioService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CardioService> _logger;
        private readonly IHelperService _helperService;
        private readonly IMongoCollection<DailyCardioDocument> _cardioDocument;
        private readonly IMongoCollection<CardioHistoryDocument> _cardioHistoryDocument;
        private readonly IMongoCollection<CardioDailyHistoryDocument> _cardioDailyHistoryDocument;
        public CardioService(AppDbContext context, ILogger<CardioService> logger, IMongoDatabase database, IHelperService helperService)
        {
            _context = context;
            _logger = logger;
            _helperService = helperService;
            _cardioDocument = database.GetCollection<DailyCardioDocument>("DailyCardio");
            _cardioHistoryDocument = database.GetCollection<CardioHistoryDocument>("CardioHistory");
            _cardioDailyHistoryDocument = database.GetCollection<CardioDailyHistoryDocument>("CardioHistoryDaily");
        }       

        public async Task<(BasicErrorResponse error, CardioTrainingDayVMO data)> GetTrainingDay(string userId, DateTime date)
        {
            try
            {
                var userCardioDocument = await _cardioDocument.Find(a=>a.UserId == userId).FirstOrDefaultAsync();

                if (userCardioDocument == null)
                {
                    _logger.LogWarning($"User cardio document not found. Creating. UserId: {userId}");
                    var newDoc = await _helperService.CreateMissingDoc(userId, _cardioDocument);
                    if (newDoc == null)
                    {
                        _logger.LogCritical($"Unable to create new cardio document for user. UserId: {userId} Method: {nameof(GetTrainingDay)}");
                        return (new BasicErrorResponse() { ErrorCode = ErrorCodes.Failed, ErrorMessage = $"User cardio docuemnt non existing.", Success = false }, new CardioTrainingDayVMO());
                    }

                    userCardioDocument = newDoc;
                }

                var targetedDay = userCardioDocument.Trainings.FirstOrDefault(a => a.Date == date);
                if (targetedDay == null && userCardioDocument.Trainings != null && userCardioDocument.Trainings.Count() >= 7)
                {
                    var oldestTraining = userCardioDocument.Trainings.OrderBy(dp => dp.Date).First();
                    await MoveDailyCardioPlanToHistory(userId, oldestTraining);
                    await MoveCardioTrainingToHistory(userId, oldestTraining); //this might cause issues -> preferably rethink its placement

                    var update = Builders<DailyCardioDocument>.Update.PullFilter(d => d.Trainings, dp => dp.Date == oldestTraining.Date);
                    await _cardioDocument.UpdateOneAsync(d => d.UserId == userId, update);

                    //insert new empty
                    DailyCardioPlan trainingUpd = new DailyCardioPlan()
                    {
                        Date = date,
                        Exercises = new List<CardioTraining>(),
                    };

                    var updated = Builders<DailyCardioDocument>.Update.Push(d => d.Trainings, trainingUpd);
                    await _cardioDocument.UpdateOneAsync(d => d.UserId == userId, updated);

                    return (new BasicErrorResponse() { ErrorCode = ErrorCodes.None, Success = true, ErrorMessage = "Sucess, empty" }, new CardioTrainingDayVMO() { Date = date, Exercises = new List<CardioTrainingDayExercviseVMO>() });
                }

                if(targetedDay == null && userCardioDocument.Trainings.Count < 7)
                {
                    //insert new empty
                    DailyCardioPlan trainingUpd = new DailyCardioPlan()
                    {
                        Date = date,
                        Exercises = new List<CardioTraining>(),
                    };

                    var updated = Builders<DailyCardioDocument>.Update.Push(d => d.Trainings, trainingUpd);
                    await _cardioDocument.UpdateOneAsync(d => d.UserId == userId, updated);

                    return (new BasicErrorResponse() { ErrorCode = ErrorCodes.None, Success = true, ErrorMessage = "Sucess, empty" }, new CardioTrainingDayVMO() { Date = date, Exercises = new List<CardioTrainingDayExercviseVMO>() });
                }

                var vmo = new CardioTrainingDayVMO() { Date = date, Exercises = new List<CardioTrainingDayExercviseVMO>() };

                var exerciseType = targetedDay.Exercises.Select(a => a.ActivityType).Distinct().ToList();
                Dictionary<ActivityType, PastCardioTrainingData> pastData = new Dictionary<ActivityType, PastCardioTrainingData>();
                foreach (var activityType in exerciseType)
                {
                    pastData[activityType] = await GetPastCardioData(userId, activityType);
                }

                foreach (var activity in targetedDay.Exercises)
                {
                    pastData.TryGetValue(activity.ActivityType, out var history);

                    var trainingRec = new CardioTrainingDayExercviseVMO()
                    {
                        ExerciseData = activity,
                        PastData = new PastCardioTrainingData()
                        {
                            AvgHeartRate = history.AvgHeartRate,
                            DistanceMeters = history.DistanceMeters,
                            Duration = history.Duration,
                            SpeedKmh = history.SpeedKmh,
                        }
                    };

                    vmo.Exercises.Add(trainingRec);
                }

                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.None, ErrorMessage = "Sucess", Success = true }, vmo);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to retrive cardio training data for UserId: {userId} Date: {date} Method: {nameof(GetTrainingDay)}");
                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"Error occured: {ex.Message}", Success = false }, new CardioTrainingDayVMO());
            }
        }

        public async Task<BasicErrorResponse> AddExerciseToTrainingDay(string userId, AddCardioExerciseVM model)
        {
            try
            {
                var userCardioDocument = await _cardioDocument.Find(a=>a.UserId == userId).FirstOrDefaultAsync();
                if(userCardioDocument == null)
                {
                    _logger.LogWarning($"User cardio-training document not found. UserId: {userId} Method: {nameof(AddExerciseToTrainingDay)}");

                    var newDoc = await _helperService.CreateMissingDoc(userId, _cardioDocument);
                    if (newDoc == null)
                    {
                        _logger.LogCritical($"Cardio document not found UserId: {userId} Method: {nameof(AddExerciseToTrainingDay)}");
                        return new BasicErrorResponse() { Success = false, ErrorCode = ErrorCodes.NotFound, ErrorMessage = "User daily cardio training document not found, couldnt perform any action." };
                    }

                    userCardioDocument = newDoc;
                }

                var targetedDay = userCardioDocument.Trainings.FirstOrDefault(a=>a.Date.Date == model.Date.Date);
                if(targetedDay == null)
                {
                    _logger.LogWarning($"cardio day with given date not found for user. UserId: {userId} Date: {model.Date}");
                    return new BasicErrorResponse() { Success = false, ErrorCode = ErrorCodes.NotFound, ErrorMessage = "Error occured. User does not have training day for given day." };
                }

                if (!model.IsMetric)
                {
                    model.Distance = model.Distance * 0.3048;
                    model.Speed = model.Speed * 1.60934;
                }

                int nextPublicId = targetedDay.Exercises.Select(a => a.PublicId).DefaultIfEmpty(0).Max() + 1;

                var newCardioRecord = new CardioTraining()
                {
                    PublicId = nextPublicId,
                    Name = model.Name ?? $"Exercise {model.ActivityType}",
                    Desc = model.Desc,
                    PrivateNotes = model.PrivateNotes,
                    ExerciseFeeling = model.ExerciseFeeling,
                    ActivityType = model.ActivityType,
                    AvgHeartRate = model.AvgHeartRate,
                    DistanceMeters = model.Distance,
                    SpeedKmH = model.Speed,
                    Duration = model.Duration,
                    ExerciseVisilibity = model.ExerciseVisilibity,
                    Route = model.EncodedRoute,
                    CaloriesBurnt = model.CaloriesBurnt,
                    FeelingPercentage = model.FeelingPercentage,
                    HeartRateInTime = model.HeartRateInTime,
                    SpeedInTime = model.SpeedInTime,
                };

                targetedDay.Exercises.Add(newCardioRecord);
                await _cardioDocument.ReplaceOneAsync(a => a.UserId == userId, userCardioDocument);

                return new BasicErrorResponse() { Success = true, ErrorCode = ErrorCodes.None, ErrorMessage = "Sucesss" };
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to add exercise to training day. UserId: {userId} Data: {model} Method: {nameof(AddExerciseToTrainingDay)}");
                return new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"Error ocdcured: {ex.Message}", Success = false };
            }
        }
        public async Task<BasicErrorResponse> JoinChallenge(string userId, int challengeId)
        {
            try
            {
                var challenge = await _context.Challanges.FirstOrDefaultAsync(a=>a.Id == challengeId);
                if(challenge == null)
                {
                    _logger.LogWarning($"User tried to join un-existing challenge. UserId: {userId} ChallengeId: {challengeId} Method: {nameof(JoinChallenge)}");
                    return new BasicErrorResponse() { ErrorCode = ErrorCodes.NotFound, ErrorMessage = $"Challenge with id {challengeId} doesn ot exists.", Success = false };
                }

                var user = await _context.AppUser.FirstOrDefaultAsync(a=>a.Id == userId);

                var newActiveChallengeRecord = new ActiveChallange()
                {
                    StartDate = DateTime.Now,
                    Challenge = challenge,
                    ChallengeId = challengeId,
                    CurrentProgress = 0
                };

                user.ActiveChallanges.Add(newActiveChallengeRecord);
                await _context.SaveChangesAsync();

                return new BasicErrorResponse() { ErrorCode = ErrorCodes.None, Success = true, ErrorMessage = "Sucess" };
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to join challenge. UserId: {userId} ChallengeId: {challengeId} Method: {nameof(JoinChallenge)}");
                return new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"Error occured: {ex.Message}", Success = false };
            }
        }


        private static GeoJsonLineString<GeoJson2DCoordinates> DecodeCordsAndConvertToMongo(string encoded)
        {
            if (string.IsNullOrWhiteSpace(encoded))
            {
                return new GeoJsonLineString<GeoJson2DCoordinates>(new GeoJsonLineStringCoordinates<GeoJson2DCoordinates>(Enumerable.Empty<GeoJson2DCoordinates>()));
            }

            var poly = new List<(double lat, double lng)>();
            int index = 0, len = encoded.Length;
            int lat = 0, lng = 0;

            while (index < len)
            {
                int b, shift = 0, result = 0;
                do
                {
                    b = encoded[index++] - 63;
                    result |= (b & 0x1F) << shift;
                    shift += 5;
                } while (b >= 0x20);
                lat += ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));

                shift = 0;
                result = 0;
                do
                {
                    b = encoded[index++] - 63;
                    result |= (b & 0x1F) << shift;
                    shift += 5;
                } while (b >= 0x20);
                lng += ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));

                poly.Add((lat / 1E5, lng / 1E5));
            }

            var geoCoords = poly.Select(p => new GeoJson2DCoordinates(p.lng, p.lat)).ToList();

            var lineStringCoords = new GeoJsonLineStringCoordinates<GeoJson2DCoordinates>(geoCoords);
            return new GeoJsonLineString<GeoJson2DCoordinates>(lineStringCoords);
        }

        private async Task MoveDailyCardioPlanToHistory(string userId, DailyCardioPlan trainings)
        {
            var userCardioDailyHistoryDoc = await _cardioDailyHistoryDocument.Find(a=>a.UserId == userId).FirstOrDefaultAsync();
            if(userCardioDailyHistoryDoc == null)
            {
                var newHistoryDoc = new CardioDailyHistoryDocument()
                {
                    UserId = userId,
                    Trainings = new List<CardioDailyHistoryTraining>(),
                };

                foreach(var training in trainings.Exercises)
                {
                    var newExercise = new CardioDailyHistoryTraining()
                    {
                        Date = trainings.Date,
                        CardioTraining = training,
                    };

                    newHistoryDoc.Trainings.Add(newExercise);
                }

                await _cardioDailyHistoryDocument.InsertOneAsync(newHistoryDoc);
                return;
            }

            foreach(var training in trainings.Exercises)
            {
                var newExercise = new CardioDailyHistoryTraining()
                {
                    Date = trainings.Date,
                    CardioTraining = training,
                };

                userCardioDailyHistoryDoc.Trainings.Add(newExercise);
            }

            await _cardioDailyHistoryDocument.ReplaceOneAsync(d => d.UserId == userId, userCardioDailyHistoryDoc);
        }

        private async Task MoveCardioTrainingToHistory(string userId, DailyCardioPlan training)
        {
            var userCardioHistoryDocument = await _cardioHistoryDocument.Find(d => d.UserId == userId).FirstOrDefaultAsync();

            if (userCardioHistoryDocument == null)
            {
                var cardioHistoryDoc = new CardioHistoryDocument
                {
                    UserId = userId,
                    Exercises = new List<CardioHistoryExercise>()
                };

                foreach (var activity in training.Exercises)
                {
                    var newExerciseGroup = new CardioHistoryExercise
                    {
                        ActivityType = activity.ActivityType,
                        ExercisesData = new List<HistoryCardioExerciseData>
                        {
                            new HistoryCardioExerciseData
                            {
                                AvgHeartRate        = activity.AvgHeartRate,
                                Date                = training.Date,
                                DistanceMeters      = activity.DistanceMeters,
                                Duration            = activity.Duration,
                                SpeedKmH            = activity.SpeedKmH,
                                CaloriesBurnt       = activity.CaloriesBurnt,
                            }
                        }
                    };
                    cardioHistoryDoc.Exercises.Add(newExerciseGroup);
                }

                await _cardioHistoryDocument.InsertOneAsync(cardioHistoryDoc);
                return;
            }

            foreach (var activity in training.Exercises)
            {
                var existingGroup = userCardioHistoryDocument.Exercises.FirstOrDefault(e => e.ActivityType == activity.ActivityType);

                var newRecord = new HistoryCardioExerciseData
                {
                    AvgHeartRate = activity.AvgHeartRate,
                    Date = training.Date,
                    DistanceMeters = activity.DistanceMeters,
                    Duration = activity.Duration,
                    SpeedKmH = activity.SpeedKmH,
                    CaloriesBurnt = activity.CaloriesBurnt
                };

                if (existingGroup != null)
                {
                    existingGroup.ExercisesData.Add(newRecord);
                }
                else
                {
                    var newGroup = new CardioHistoryExercise
                    {
                        ActivityType = activity.ActivityType,
                        ExercisesData = new List<HistoryCardioExerciseData> { newRecord }
                    };

                    userCardioHistoryDocument.Exercises.Add(newGroup);
                }
            }

            await _cardioHistoryDocument.ReplaceOneAsync(d => d.UserId == userId, userCardioHistoryDocument);
        }

        private async Task<PastCardioTrainingData> GetPastCardioData(string userId, ActivityType type)
        {
            var pastData = new PastCardioTrainingData()
            {
                AvgHeartRate = 0,
                SpeedKmh = 0,
                DistanceMeters = 0,
                Duration = TimeSpan.Zero,
                CaloriesBurnt = 0,
            };

            var userHistoryDoc = await _cardioHistoryDocument.Find(a=>a.UserId == userId).FirstOrDefaultAsync();
            if (userHistoryDoc != null)
            {
                var target = userHistoryDoc.Exercises.Find(a=>a.ActivityType == type);
                if(target != null)
                {
                    var mostRecent = target.ExercisesData.OrderByDescending(e => e.Date).First();
                    pastData.AvgHeartRate = mostRecent.AvgHeartRate;
                    pastData.Duration = mostRecent.Duration;
                    pastData.DistanceMeters = mostRecent.DistanceMeters;
                    pastData.SpeedKmh = mostRecent.SpeedKmH;
                    pastData.CaloriesBurnt = mostRecent.CaloriesBurnt;
                }
            }

            return pastData;
        }

        public async Task<BasicErrorResponse> ChangeExerciseVisilibity(string userId, ChangeExerciseVisilibityVM model)
        {
            try
            {
                var userCardioDocument = await _cardioDocument.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if (userCardioDocument == null)
                {
                    _logger.LogWarning($"User daily cardio document non existing. creating. UserId: {userId} Method: {nameof(ChangeExerciseVisilibity)}");
                    await _helperService.CreateMissingDoc(userId, _cardioDocument);

                    return new BasicErrorResponse() { ErrorCode = ErrorCodes.NotFound, ErrorMessage = "Couldnt perform patch. User cardio document not found.", Success = false };
                }

                var targetDay = userCardioDocument.Trainings.FirstOrDefault(a => a.Date.Date == model.Date.Date);
                if (targetDay == null)
                {
                    _logger.LogWarning($"Couldnt find cardio training for user in given period. UserId: {userId} Date: {model.Date} Method: {nameof(ChangeExerciseVisilibity)}");
                    return new BasicErrorResponse() { Success = false, ErrorCode = ErrorCodes.NotFound, ErrorMessage = "Couldnt find and trainings for given date. Date invalid." };
                }

                var targetExercise = targetDay.Exercises.FirstOrDefault(a => a.PublicId == model.ExerciseId);
                if (targetExercise == null)
                {
                    _logger.LogWarning($"Couldn't find exercise with given id in user cardio training. Date: {model.Date} ExerciseId: {model.ExerciseId} Method: {nameof(ChangeExerciseVisilibity)}");
                    return new BasicErrorResponse() { Success = false, ErrorCode = ErrorCodes.NotFound, ErrorMessage = "Couldn't find exercise with given id in user cardio training." };
                }

                targetExercise.ExerciseVisilibity = model.State;

                var res = await _cardioDocument.ReplaceOneAsync(d => d.Id == userCardioDocument.Id, userCardioDocument);
                if (!res.IsAcknowledged && res.ModifiedCount != 1)
                {
                    return new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = "Update faild.", Success = false };
                }

                return new BasicErrorResponse() { ErrorCode = ErrorCodes.None, ErrorMessage = "Sucess", Success = true };
            } 
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Couldnt change exercise visilibity. UserId: {userId} Data: {model} Method: {nameof(ChangeExerciseVisilibity)}");
                return new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An error occured: {ex.Message}", Success = false };
            }
        }

        public async Task<BasicErrorResponse> DeleteExercisesFromCardioTrainingDay(string userId, DeleteExercisesFromCardioTrainingVM model)
        {
            try
            {
                var userCardioDocument = await _cardioDocument.Find(a=>a.UserId == userId).FirstOrDefaultAsync();
                if (userCardioDocument == null)
                {
                    _logger.LogWarning($"User daily cardio document non existing. creating. UserId: {userId} Method: {nameof(DeleteExercisesFromCardioTrainingDay)}");
                    await _helperService.CreateMissingDoc(userId, _cardioDocument);

                    return new BasicErrorResponse() { ErrorCode = ErrorCodes.NotFound, ErrorMessage = "Couldnt perform deletion. User cardio document not found.", Success = false };
                }

                var targetDay = userCardioDocument.Trainings.FirstOrDefault(a=>a.Date.Date == model.Date.Date);
                if(targetDay == null)
                {
                    _logger.LogWarning($"Couldnt find cardio training for user in given period. UserId: {userId} Date: {model.Date} Method: {nameof(DeleteExercisesFromCardioTrainingDay)}");
                    return new BasicErrorResponse() { Success = false, ErrorCode = ErrorCodes.NotFound, ErrorMessage = "Couldnt find and trainings for given date. Date invalid." };
                }

                var removedCount = targetDay.Exercises.RemoveAll(e => model.ExercisesIdToRemove.Contains(e.PublicId));
                if (removedCount == 0)
                {
                    _logger.LogWarning("No exercises removed: no matching IDs found for UserId {UserId} on Date {Date}", userId, model.Date);
                }

                await _cardioDocument.ReplaceOneAsync(a => a.UserId == userId, userCardioDocument);
                return new BasicErrorResponse() { Success = true, ErrorCode = ErrorCodes.None, ErrorMessage = "Sucesss" };

            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to remove exercises from cardio training day. UserId: {userId} Data: {model} Method: {nameof(DeleteExercisesFromCardioTrainingDay)}");
                return new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An error occured: {ex.Message}", Success = false };
            }
        }
       
    }
}
