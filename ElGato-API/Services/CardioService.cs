using ElGato_API.Data;
using ElGato_API.Interfaces;
using ElGato_API.Models.User;
using ElGato_API.ModelsMongo.Cardio;
using ElGato_API.ModelsMongo.Training;
using ElGato_API.VM.Cardio;
using ElGato_API.VMO.Cardio;
using ElGato_API.VMO.ErrorResponse;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using System.Text;

namespace ElGato_API.Services
{
    public class CardioService : ICardioService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CardioService> _logger;
        private readonly IHelperService _helperService;
        private readonly IMongoCollection<DailyCardioDocument> _cardioDocument;
        public CardioService(AppDbContext context, ILogger<CardioService> logger, IMongoDatabase database, IHelperService helperService)
        {
            _context = context;
            _logger = logger;
            _helperService = helperService;
            _cardioDocument = database.GetCollection<DailyCardioDocument>("DailyCardio");
        }

        public async Task<(BasicErrorResponse error, List<ChallengeVMO>? data)> GetActiveChallenges(string userId)
        {
            try
            {
                List<ChallengeVMO> vmo = new List<ChallengeVMO>();
                var challs = await _context.Challanges.Where(a=>a.EndDate > DateTime.Today).ToListAsync();
                var participatedChallenges = await _context.AppUser.Include(c=>c.ActiveChallanges).Where(a=>a.Id == userId).FirstOrDefaultAsync();

                if (participatedChallenges != null && participatedChallenges.ActiveChallanges != null)
                {
                    challs.RemoveAll(c => participatedChallenges.ActiveChallanges.Any(ac => ac.ChallengeId == c.Id));
                }

                foreach (var chall in challs)
                {
                    var newRec = new ChallengeVMO()
                    {
                        Id = chall.Id,
                        Name = chall.Name,
                        Badge = chall.Badge,
                        Description = chall.Description,
                        EndDate = chall.EndDate,
                        GoalType = chall.GoalType,
                        GoalValue = chall.GoalValue,
                        MaxTimeMinutes = chall.MaxTimeMinutes,
                        Type = chall.Type
                    };

                    if (chall.Creator != null)
                    {
                        newRec.Creator = new CreatorVMO()
                        {
                            Description = chall.Creator.Description,
                            Name = chall.Creator.Name,
                            Pfp = chall.Creator.Pfp,
                        };
                    }

                    vmo.Add(newRec);
                }

                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.None, ErrorMessage = "Sucess", Success = true}, vmo);
            } 
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get active challenges. Method: {nameof(GetActiveChallenges)}");
                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"Error occured: {ex.Message}", Success = false }, null);
            }
        }

        public async Task<(BasicErrorResponse error, List<ActiveChallengeVMO>? data)> GetUserActiveChallenges(string userId)
        {
            try
            {
                var vmo = new List<ActiveChallengeVMO>();
                var user = await _context.AppUser.Include(a=>a.ActiveChallanges).ThenInclude(ac => ac.Challenge).FirstOrDefaultAsync(a=>a.Id == userId);
                if (user != null && user.ActiveChallanges != null)
                {
                    foreach (var activeChallenge in user.ActiveChallanges)
                    {
                        if(activeChallenge.Challenge.EndDate < DateTime.UtcNow)
                        {
                            continue;
                        }

                        var challengeVMO = new ChallengeVMO
                        {
                            Id = activeChallenge.Challenge.Id,
                            Name = activeChallenge.Challenge.Name,
                            Description = activeChallenge.Challenge.Description,
                            EndDate = activeChallenge.Challenge.EndDate,
                            Badge = activeChallenge.Challenge.Badge,    
                            GoalType = activeChallenge.Challenge.GoalType,
                            GoalValue = activeChallenge.Challenge.GoalValue,
                            MaxTimeMinutes = activeChallenge.Challenge.MaxTimeMinutes,
                            Type = activeChallenge.Challenge.Type
                        };

                        if(activeChallenge.Challenge.Creator != null) 
                        {
                            var creator = new CreatorVMO()
                            {
                                Description = activeChallenge.Challenge.Creator.Description,
                                Name = activeChallenge.Challenge.Creator.Name,
                                Pfp = activeChallenge.Challenge.Creator.Pfp,
                            };

                            challengeVMO.Creator = creator;
                        }

                        vmo.Add(new ActiveChallengeVMO
                        {
                            ChallengeData = challengeVMO,
                            CurrentProgess = activeChallenge.CurrentProgress,
                            StartDate = activeChallenge.StartDate
                        });
                    }
                }

                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.None, Success = true, ErrorMessage = "Sucess"}, vmo);
            } 
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get currently active challenges for user. UserId: {userId} Method: {nameof(GetActiveChallenges)}");
                return (new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"Error occured: {ex.Message}", Success = false }, null);
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

                var targetedDay = userCardioDocument.Trainings.FirstOrDefault(a=>a.Date == model.Date);
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
                    Route = DecodeCordsAndConvertToMongo(model.EncodedRoute),
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


    }
}
