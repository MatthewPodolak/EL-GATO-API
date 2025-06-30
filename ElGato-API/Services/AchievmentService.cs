using ElGato_API.Data;
using ElGato_API.Interfaces;
using ElGato_API.Migrations;
using ElGato_API.Models.Feed;
using ElGato_API.Models.User;
using ElGato_API.ModelsMongo.Cardio;
using ElGato_API.VM.Achievments;
using ElGato_API.VM.UserData;
using ElGato_API.VMO.Achievments;
using ElGato_API.VMO.Cardio;
using ElGato_API.VMO.ErrorResponse;
using Microsoft.EntityFrameworkCore;

namespace ElGato_API.Services
{
    public class AchievmentService : IAchievmentService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AchievmentService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IUserService _userService;

        public AchievmentService(AppDbContext context, ILogger<AchievmentService> logger, IServiceScopeFactory scopeFactory, IUserService userService) 
        { 
            _context = context;
            _logger = logger;
            _scopeFactory = scopeFactory;
            _userService = userService;
        }

        public async Task<(ErrorResponse error, string? achievmentName)> GetCurrentAchivmentIdFromFamily(string achievmentFamily, string userId, AppDbContext? context)
        {
            AppDbContext dbContext;

            if(context == null)
            {
                using var scope = _scopeFactory.CreateScope();
                dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            }
            else
            {
                dbContext = context;
            }
         

            try
            {
                var user = await dbContext.AppUser.Include(a => a.Achievments).Where(a => a.Id == userId).FirstOrDefaultAsync();
                if (user == null)
                {
                    _logger.LogWarning($"User not found while trying inside GetCurrentAchivmentIdFromFamily, for user {userId}");
                    return (ErrorResponse.NotFound("User not found"), null);
                }

                var relevantAchievements = user.Achievments.Where(ach => ach.Family == achievmentFamily).ToList();

                if (!relevantAchievements.Any())
                {
                    return (new ErrorResponse { Success = true }, $"{achievmentFamily}_0");
                }

                var maxAchievement = relevantAchievements
                    .OrderByDescending(ach =>
                    {
                        var parts = ach.StringId.Split('_');
                        return parts.Length > 1 && int.TryParse(parts[1], out int number) ? number : 0;
                    })
                    .FirstOrDefault();

                int currentMax = 0;
                if (maxAchievement != null)
                {
                    var parts = maxAchievement.StringId.Split('_');
                    if (parts.Length > 1 && int.TryParse(parts[1], out int number))
                    {
                        currentMax = number;
                    }
                }

                string currentAchievmentName = $"{achievmentFamily}_{currentMax + 1}";

                var doesAchievmentExist = await dbContext.Achievment.FirstOrDefaultAsync(a => a.StringId == currentAchievmentName);
                if(doesAchievmentExist == null) { return (new ErrorResponse() { Success = true }, null); }

                return (ErrorResponse.Ok(), currentAchievmentName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetCurrentAchivmentIdFromFamily internal error");
                return (ErrorResponse.Internal(ex.Message), null);
            }
        }

        public async Task<(ErrorResponse error, int value)> GetPreviousCounterValue(string achievmentStringId, string userId, AppDbContext? context)
        {
            AppDbContext dbContext;

            if (context == null)
            {
                using var scope = _scopeFactory.CreateScope();
                dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            }
            else
            {
                dbContext = context;
            }

            try
            {
                Achievment achievmentModel;
                int number = 0;

                achievmentModel = await dbContext.Achievment.FirstOrDefaultAsync(a => a.StringId == achievmentStringId);
                if(achievmentModel == null)
                {
                    string fallbackId;

                    if (!string.IsNullOrEmpty(achievmentStringId))
                    {
                        var parts = achievmentStringId.Split('_', 2);
                        var prefix = parts[0];

                        if (parts.Length == 2 && Int32.TryParse(parts[1], out number))
                        {
                            if(number <= 0)
                            {
                                return (ErrorResponse.Ok(), 0);
                            }

                            fallbackId = $"{prefix}_{number - 1}";
                        }
                        else
                        {
                            _logger.LogError("Failex while trying to retrive ach id prefix.");
                            return (ErrorResponse.Failed("Failex while trying to retrive ach id prefix."), 0);
                        }
                    }
                    else
                    {
                        _logger.LogError("Failex while trying to retrive ach id prefix.");
                        return (ErrorResponse.Failed("Failex while trying to retrive ach id prefix."), 0);
                    }

                    achievmentModel = await dbContext.Achievment.FirstOrDefaultAsync(a => a.StringId == fallbackId);
                    if(achievmentModel == null)
                    {
                        _logger.LogError($"Failed while tryinh to get current achievment counter. Method: {nameof(GetPreviousCounterValue)}");
                        return (ErrorResponse.Failed("Failed while tryinh to get current achievment counter."), 0);
                    }
                }

                var userCounter = await dbContext.AchievementCounters.FirstOrDefaultAsync(a => a.UserId == userId && a.AchievmentId == achievmentModel.Id);
                if(userCounter == null)
                {
                    if(number == 0) {  return (ErrorResponse.Ok(), 0); }

                    _logger.LogError($"Failed while tryinh to get current achievment counter. Method: {nameof(GetPreviousCounterValue)}");
                    return (ErrorResponse.Failed("Failed while tryinh to get current achievment counter."), 0);
                }

                return (ErrorResponse.Ok(), userCounter.Counter);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"An error occured while trying to get previous achievment counter value. Method: {nameof(GetPreviousCounterValue)}");
                return (ErrorResponse.Internal(ex.Message), 0);
            }
        }

        public async Task<(ErrorResponse error, AchievmentResponse? ach)> IncrementAchievmentProgress(string achievmentStringId, string userId, int incValue, AppDbContext? context)
        {
            AppDbContext dbContext;

            if (context == null)
            {
                using var scope = _scopeFactory.CreateScope();
                dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            }
            else
            {
                dbContext = context;
            }

            try
            {
                AchievmentResponse achRes = new AchievmentResponse() { Achievment = new AchievmentVMO(), Status = new ErrorResponse() };

                var achievment = await dbContext.Achievment.FirstOrDefaultAsync(a => a.StringId == achievmentStringId);
                if (achievment == null) { _logger.LogWarning($"User {userId} attempted to access non-existent achievement {achievmentStringId}"); return (new ErrorResponse() { Success = false, ErrorMessage = "Given achievments does not exists." }, null); }

                var userCount = await dbContext.AchievementCounters.FirstOrDefaultAsync(a => a.UserId == userId && a.AchievmentId == achievment.Id);
                if (userCount == null)
                {
                    AchievementCounter counter = new AchievementCounter()
                    {
                        Counter = incValue,
                        AchievmentId = achievment.Id,
                        UserId = userId,
                    };

                    await dbContext.AchievementCounters.AddAsync(counter);

                    if (achievment.Threshold <= incValue)
                    {
                        achRes.Achievment.ExceededThreshold = achievment.Threshold;
                        achRes.Achievment.AchievmentEarnedName = achievment.Name;
                        achRes.Achievment.AchievmentEarnedImage = achievment.Img;
                        achRes.Achievment.GenerativeText = achievment.GenerativeText;

                        var user = await dbContext.Users.Include(u => u.Achievments).FirstOrDefaultAsync(u => u.Id == userId);
                        if (user != null)
                        {
                            if (user.Achievments == null)
                            {
                                user.Achievments = new List<Achievment>();
                            }
                            user.Achievments.Add(achievment);                            
                            await dbContext.SaveChangesAsync();
                        }

                        achRes.Status.Success = true;
                        achRes.Status.ErrorMessage = "Sucess";
                        achRes.Status.ErrorCode = ErrorCodes.None;
                        return (new ErrorResponse() { Success = true, }, achRes);
                    }

                    await dbContext.SaveChangesAsync();
                    return (ErrorResponse.Ok(), new AchievmentResponse() { Status = ErrorResponse.Ok() });
                }
                else
                {
                    if(achievment.DailyLimit && userCount.LastCount.Date == DateTime.Today)
                    {
                        return (ErrorResponse.Ok(), new AchievmentResponse() { Status = ErrorResponse.Ok() });
                    }

                    userCount.Counter += incValue;
                    userCount.LastCount = DateTime.Today;
                    await dbContext.SaveChangesAsync();

                    if (achievment.Threshold == userCount.Counter) 
                    {
                        achRes.Achievment.ExceededThreshold = achievment.Threshold;
                        achRes.Achievment.AchievmentEarnedName = achievment.Name;
                        achRes.Achievment.AchievmentEarnedImage = achievment.Img;
                        achRes.Achievment.GenerativeText = achievment.GenerativeText;

                        var user = await dbContext.Users.Include(u => u.Achievments).FirstOrDefaultAsync(u => u.Id == userId);
                        if (user != null)
                        {
                            if (user.Achievments == null)
                            {
                                user.Achievments = new List<Achievment>();
                            }
                            user.Achievments.Add(achievment);
                            await dbContext.SaveChangesAsync();
                        }

                        achRes.Status.Success = true;
                        achRes.Status.ErrorMessage = "Sucess";
                        achRes.Status.ErrorCode = ErrorCodes.None;
                        return (ErrorResponse.Ok(), achRes);
                    }

                    return (ErrorResponse.Ok(), new AchievmentResponse() { Status = ErrorResponse.Ok() });
                }

            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, $"Failed to increment achievment progress. -- IncrementAchievmentProgress");
                return (ErrorResponse.Internal(ex.Message), null);
            }
        }

        public async Task<(ErrorResponse error, List<ChallengeVMO>? data)> GetActiveChallenges(string userId)
        {
            try
            {
                List<ChallengeVMO> vmo = new List<ChallengeVMO>();
                var challs = await _context.Challanges.Where(a => a.EndDate > DateTime.Today).ToListAsync();
                var participatedChallenges = await _context.AppUser.Include(c => c.ActiveChallanges).Where(a => a.Id == userId).FirstOrDefaultAsync();

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

                return (ErrorResponse.Ok(), vmo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get active challenges. Method: {nameof(GetActiveChallenges)}");
                return (ErrorResponse.Internal(ex.Message), null);
            }
        }

        public async Task<(ErrorResponse error, List<ActiveChallengeVMO>? data)> GetUserActiveChallenges(string userId)
        {
            try
            {
                var vmo = new List<ActiveChallengeVMO>();
                var user = await _context.AppUser.Include(a => a.ActiveChallanges).ThenInclude(ac => ac.Challenge).FirstOrDefaultAsync(a => a.Id == userId);
                if (user != null && user.ActiveChallanges != null)
                {
                    foreach (var activeChallenge in user.ActiveChallanges)
                    {
                        if (activeChallenge.Challenge.EndDate < DateTime.UtcNow)
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

                        if (activeChallenge.Challenge.Creator != null)
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

                return (ErrorResponse.Ok(), vmo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get currently active challenges for user. UserId: {userId} Method: {nameof(GetActiveChallenges)}");
                return (ErrorResponse.Internal(ex.Message), null);
            }
        }

        public async Task<ErrorResponse> CheckAndAddBadgeProgressForUser(string userId, BadgeIncDataVM model, AppDbContext? context = null)
        {
            try
            {
                var user = context != null ? await context.AppUser.Include(a => a.UserBadges).Include(a => a.ActiveChallanges).ThenInclude(ac => ac.Challenge)
                    .FirstOrDefaultAsync(a => a.Id == userId) : await _context.AppUser.Include(a => a.UserBadges).Include(a => a.ActiveChallanges).ThenInclude(ac => ac.Challenge)
                    .FirstOrDefaultAsync(a => a.Id == userId);

                if (user == null || user.ActiveChallanges == null)
                {
                    return new ErrorResponse
                    {
                        ErrorCode = ErrorCodes.None,
                        Success = true,
                        ErrorMessage = "Success, active challenges not found."
                    };
                }

                if (user.UserBadges == null)
                {
                    user.UserBadges = new List<UserBadges>();
                }

                var activeChallanges = user.ActiveChallanges.Where(ac => ac.Challenge.EndDate != null && ac.Challenge.EndDate > DateTime.UtcNow).ToList();

                var completedChallenges = new List<ActiveChallange>();

                foreach (var activeChallange in activeChallanges)
                {
                    switch (activeChallange.Challenge.GoalType)
                    {
                        case ChallengeGoalType.TotalDistanceKm:
                            switch (activeChallange.Challenge.Type)
                            {
                                case ChallangeType.Running:
                                    if (model.ActivityType == ActivityType.Running)
                                        activeChallange.CurrentProgress += model.Distance;
                                    break;
                                case ChallangeType.Swimming:
                                    if (model.ActivityType == ActivityType.Swimming)
                                        activeChallange.CurrentProgress += model.Distance;
                                    break;
                                case ChallangeType.Walking:
                                    if (model.ActivityType == ActivityType.Walking)
                                        activeChallange.CurrentProgress += model.Distance;
                                    break;
                                case ChallangeType.Bike:
                                    if (model.ActivityType == ActivityType.Bike || model.ActivityType == ActivityType.MountainBike)
                                        activeChallange.CurrentProgress += model.Distance;
                                    break;
                                case ChallangeType.March:
                                    if (model.ActivityType == ActivityType.Walking)
                                        activeChallange.CurrentProgress += model.Distance;
                                    break;
                                case ChallangeType.None:
                                    activeChallange.CurrentProgress += model.Distance;
                                    break;
                            }
                            break;

                        case ChallengeGoalType.TotalActivities:
                            activeChallange.CurrentProgress += 1;
                            break;

                        case ChallengeGoalType.TotalCalories:
                            activeChallange.CurrentProgress += model.CaloriesBurnt;
                            break;

                        case ChallengeGoalType.TotalElevation:
                            activeChallange.CurrentProgress += model.Elevation;
                            break;
                    }

                    if (activeChallange.CurrentProgress >= activeChallange.Challenge.GoalValue)
                    {
                        completedChallenges.Add(activeChallange);
                        user.UserBadges.Add(new UserBadges
                        {
                            Challange = activeChallange.Challenge,
                            ChallangeId = activeChallange.ChallengeId,
                            CompletedTime = DateTime.UtcNow,
                            User = user,
                            UserId = userId
                        });
                    }
                }

                foreach (var challenge in completedChallenges)
                {
                    user.ActiveChallanges.Remove(challenge);
                }

                var save = context != null ? await context.SaveChangesAsync() : await _context.SaveChangesAsync();

                return ErrorResponse.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to check and add badge progress UserId: {userId} Method: {nameof(CheckAndAddBadgeProgressForUser)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<(ErrorResponse error, AchievmentResponse? ach)> AddFromHealthConnectToStatisticsAndIncrementAchievments(string userId, List<UserStatisticsVM> data)
        {
            try
            {
                await using var sqlTx = await _context.Database.BeginTransactionAsync();

                var sqlTask = await AddDataFromHealthConnectToAchievmentProgess(userId, data, _context);
                if (!sqlTask.error.Success)
                {
                    await sqlTx.RollbackAsync();
                    return (sqlTask.error, null);
                }

                var mongoResult = await _userService.AddToUserStatistics(userId, data);
                if (!mongoResult.Success)
                {
                    await sqlTx.RollbackAsync();
                    return (mongoResult, null);
                }

                await sqlTx.CommitAsync();
                return (ErrorResponse.Ok(), sqlTask.ach ?? null);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to add from HK to statistics and achievments. UserId: {userId} Method: {nameof(AddFromHealthConnectToStatisticsAndIncrementAchievments)}");
                return (ErrorResponse.Internal(ex.Message), null);
            }
        }

        private async Task<(ErrorResponse error, AchievmentResponse? ach)> AddDataFromHealthConnectToAchievmentProgess(string userId, List<UserStatisticsVM> data, AppDbContext context)
        {
            try
            {
                var achievmentResponse = new AchievmentResponse();

                foreach(var item in data)
                {
                    switch (item.Type)
                    {
                        case ModelsMongo.Statistics.StatisticType.CaloriesBurnt:
                            var achFamily = await GetCurrentAchivmentIdFromFamily("CALORIE", userId, context);
                            if (!achFamily.error.Success || String.IsNullOrEmpty(achFamily.achievmentName))
                            {
                                _logger.LogError($"Failed while trying to add progress ach. Method: {nameof(AddDataFromHealthConnectToAchievmentProgess)}");
                                return (ErrorResponse.Failed($"An error occured while trying to add progres."), null);
                            }

                            var achResponseCalorie = await IncrementAchievmentProgress(achFamily.achievmentName, userId, (int)item.Value, context);
                            if (!achResponseCalorie.error.Success)
                            {
                                _logger.LogError($"Failed while trying to add progress ach. Method: {nameof(AddDataFromHealthConnectToAchievmentProgess)}");
                                return (ErrorResponse.Failed("$\"An error occured while trying to add progres.\""), null);
                            }

                            if(achievmentResponse != null)
                            {
                                achievmentResponse = achResponseCalorie.ach ?? null;
                            }

                            break;
                        case ModelsMongo.Statistics.StatisticType.StepsTaken:
                            var achFamilySteps = await GetCurrentAchivmentIdFromFamily("STEPS", userId, context);
                            if (!achFamilySteps.error.Success || String.IsNullOrEmpty(achFamilySteps.achievmentName))
                            {
                                _logger.LogError($"Failed while trying to add progress ach. Method: {nameof(AddDataFromHealthConnectToAchievmentProgess)}");
                                return (ErrorResponse.Failed($"An error occured while trying to add progres."), null);
                            }

                            var achResponseSteps = await IncrementAchievmentProgress(achFamilySteps.achievmentName, userId, (int)item.Value, context);
                            if (!achResponseSteps.error.Success)
                            {
                                _logger.LogError($"Failed while trying to add progress ach. Method: {nameof(AddDataFromHealthConnectToAchievmentProgess)}");
                                return (ErrorResponse.Failed("An error occured while tryinh to add progress."), null);
                            }

                            if(achievmentResponse != null)
                            {
                                achievmentResponse = achResponseSteps.ach ?? null;
                            }
                            break;
                    }
                }

                return (ErrorResponse.Ok(), achievmentResponse);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to add data to ach progress. UserId: {userId} Method: {nameof(AddDataFromHealthConnectToAchievmentProgess)}");
                return (ErrorResponse.Internal(ex.Message), null);
            }
        }
       
    }

}
