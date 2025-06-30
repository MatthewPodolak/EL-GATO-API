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
            var client = _userStatisticsDocument.Database.Client;
            var options = new TransactionOptions(readConcern: ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority);
            const int MaxRetries = 3;

            using var session = await client.StartSessionAsync();
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    return await session.WithTransactionAsync<AchievmentResponse>(
                        async (s, ct) =>
                        {
                            var stats = new List<UserStatisticsVM>
                            {
                                new UserStatisticsVM
                                {
                                    Type  = StatisticType.StepsTaken,
                                    Date  = model.Date,
                                    Value = model.Steps
                                }
                            };

                            var addRes = await _userService.AddToUserStatistics(userId, stats, s, true);
                            if (!addRes.Success)
                                return new AchievmentResponse { Status = ErrorResponse.Failed() };

                            await using var sqlTx = await _context.Database.BeginTransactionAsync(ct);

                            var family = await _achievmentService.GetCurrentAchivmentIdFromFamily("STEPS", userId, _context);
                            if (!family.error.Success)
                            {
                                await sqlTx.RollbackAsync(ct);
                                return new AchievmentResponse { Status = ErrorResponse.Failed() };
                            }

                            if (string.IsNullOrEmpty(family.achievmentName))
                            {
                                await sqlTx.CommitAsync(ct);
                                return new AchievmentResponse { Status = ErrorResponse.Ok() };
                            }

                            var prev = await _achievmentService.GetPreviousCounterValue(family.achievmentName, userId, _context);
                            if (!prev.error.Success)
                            {
                                await sqlTx.RollbackAsync(ct);
                                return new AchievmentResponse { Status = ErrorResponse.Failed() };
                            }

                            var inc = await _achievmentService.IncrementAchievmentProgress(
                                family.achievmentName,
                                userId,
                                model.Steps - prev.value,
                                _context
                            );
                            if (!inc.error.Success)
                            {
                                await sqlTx.RollbackAsync(ct);
                                return new AchievmentResponse { Status = ErrorResponse.Failed() };
                            }

                            await sqlTx.CommitAsync(ct);
                            return inc.ach ?? new AchievmentResponse { Status = ErrorResponse.Ok() };
                        },
                        options
                    );
                }
                catch (MongoCommandException mex) when (mex.HasErrorLabel("TransientTransactionError") ||  mex.Message.Contains("Write conflict"))
                {
                    if (attempt == MaxRetries)
                        return new AchievmentResponse
                        {
                            Status = ErrorResponse.Internal($"Mongo transaction failed after {MaxRetries} attempts: {mex.Message}")
                        };

                    await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt));
                }
                catch (Exception ex)
                {
                    return new AchievmentResponse
                    {
                        Status = ErrorResponse.Internal(ex.Message)
                    };
                }
            }

            return new AchievmentResponse
            {
                Status = ErrorResponse.Internal("Unexpected retry logic path.")
            };
        }



    }
}
