﻿using Azure.Core;
using ElGato_API.Data;
using ElGato_API.Interfaces;
using ElGato_API.Models.Feed;
using ElGato_API.Models.User;
using ElGato_API.ModelsMongo.Cardio;
using ElGato_API.ModelsMongo.History;
using ElGato_API.ModelsMongo.Statistics;
using ElGato_API.ModelsMongo.Training;
using ElGato_API.Services.Helpers;
using ElGato_API.VM.Community;
using ElGato_API.VMO.Cardio;
using ElGato_API.VMO.Community;
using ElGato_API.VMO.ErrorResponse;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElGato_API.Services
{
    public class CommunityService : ICommunityService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CommunityService> _logger;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly IMongoCollection<DailyCardioDocument> _cardioDocument;
        private readonly IMongoCollection<CardioDailyHistoryDocument> _cardioDailyHistoryDocument;
        private readonly IMongoCollection<CardioHistoryDocument> _cardioHistoryDocument;
        private readonly IMongoCollection<DailyTrainingDocument> _trainingCollection;
        private readonly IMongoCollection<TrainingHistoryDocument> _trainingHistoryCollection;
        private readonly IMongoCollection<ExercisesHistoryDocument> _exerciseHistory;
        private readonly IMongoCollection<UserStatisticsDocument> _userStatisticsDocument;
        private readonly IHelperService _helperService;
        public CommunityService(AppDbContext context, ILogger<CommunityService> logger, IDbContextFactory<AppDbContext> contextFactory, IMongoDatabase database, IHelperService helperService) 
        { 
            _context = context;
            _logger = logger;
            _contextFactory = contextFactory;
            _cardioDocument = database.GetCollection<DailyCardioDocument>("DailyCardio");
            _cardioDailyHistoryDocument = database.GetCollection<CardioDailyHistoryDocument>("CardioHistoryDaily");
            _trainingCollection = database.GetCollection<DailyTrainingDocument>("DailyTraining");
            _trainingHistoryCollection = database.GetCollection<TrainingHistoryDocument>("TrainingHistory");
            _userStatisticsDocument = database.GetCollection<UserStatisticsDocument>("Statistics");
            _cardioHistoryDocument = database.GetCollection<CardioHistoryDocument>("CardioHistory");
            _exerciseHistory = database.GetCollection<ExercisesHistoryDocument>("ExercisesHistory");
            _helperService = helperService;
        }

        public async Task<bool> UserExists(string userId)
        {
            try
            {
                using var ctx = _contextFactory.CreateDbContext();
                return await ctx.AppUser.AnyAsync(a => a.Id == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Checking if user exists FAILED. UserId: {userId} Method: {nameof(UserExists)}");
                return true;
            }
        }

        public async Task<bool> CheckIfUserIsBlockedBy(string userId, string checkingUserId)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                return await context.UserBlock.AnyAsync(b => (b.BlockerId == checkingUserId && b.BlockedId == userId));
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Checking if user is blocked FAILED. UserId: {userId} CheckingId: {checkingUserId} Method: {nameof(CheckIfUserIsBlockedBy)}");
                return true;
            }
        }

        public async Task<bool> CheckIfUserIsBlocking(string userId, string checkingUserId)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                return await context.UserBlock.AnyAsync(b =>b.BlockerId == userId && b.BlockedId == checkingUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Checking if user is blocking FAILED. UserId: {userId} CheckingId: {checkingUserId} Method: {nameof(CheckIfUserIsBlocking)}");
                return true;
            }
        }

        public async Task<bool> CheckIfUserFollowUser(string userId, string checkingUserId)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                return await context.UserFollower.AnyAsync(f => f.FolloweeId == checkingUserId && f.FollowerId == userId);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to check if user follow user. UserId: {userId} Checking: {checkingUserId} Method: {nameof(CheckIfUserFollowUser)}");
                return false;
            }
        }

        public async Task<AcessibleVMO> CheckIfProfileIsAcessibleForUser(string userAskingId, string userCheckingId)
        {
            try
            {
                var blockedByTask = Task.Run(async () =>
                {
                    return await CheckIfUserIsBlockedBy(userAskingId, userCheckingId);
                });

                var blockingTask = Task.Run(async () =>
                {
                    return await CheckIfUserIsBlocking(userAskingId, userCheckingId);
                });

                var isFriendTask = Task.Run(async () =>
                {
                    using var ctx = _contextFactory.CreateDbContext();
                    return await _context.UserFollower.AnyAsync(a => a.FollowerId == userAskingId && a.FolloweeId == userCheckingId);
                });

                var isPrivateTask = Task.Run(async () =>
                {
                    using var ctx = _contextFactory.CreateDbContext();
                    return await ctx.AppUser.Where(p => p.Id == userCheckingId).Select(p => (bool?)p.IsProfilePrivate).FirstOrDefaultAsync();
                });

                await Task.WhenAll(blockedByTask, blockingTask, isFriendTask, isPrivateTask);

                if (isPrivateTask.Result == null)
                {
                    _logger.LogWarning($"User not found: {userCheckingId} Method: {nameof(CheckIfProfileIsAcessibleForUser)}");
                    return new AcessibleVMO() { Acessible = false, UnacessilibityReason = UnacessilibityReason.Other };
                }

                if (blockedByTask.Result || blockingTask.Result)
                    return new AcessibleVMO() { Acessible = false, UnacessilibityReason = UnacessilibityReason.Blocked };

                if (isPrivateTask.Result == true && !isFriendTask.Result)
                    return new AcessibleVMO() { Acessible = false, UnacessilibityReason = UnacessilibityReason.Private };

                return new AcessibleVMO() { Acessible = true };

            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Checking if profile is acessible failed. UserId: {userAskingId} CheckingUserId: {userCheckingId} Method: {nameof(CheckIfProfileIsAcessibleForUser)}");
                return new AcessibleVMO() { Acessible = false, UnacessilibityReason = UnacessilibityReason.Other };
            }
        }

        public async Task<ErrorResponse> RequestFollow(string userAskingId, string userTargetId)
        {
            try
            {
                var requestCheck = await _context.UserFollowerRequest.FirstOrDefaultAsync(a=>a.RequesterId == userAskingId && a.TargetId == userTargetId);
                if(requestCheck != null)
                {
                    _logger.LogWarning($"User tried to request follow second time. UserId: {userAskingId} Targer: {userTargetId} Method: {nameof(RequestFollow)}");
                    return ErrorResponse.Failed("Already requested");
                }

                var newRequest = new UserFollowerRequest()
                {
                    IsPending = true,
                    RequestedAt = DateTime.UtcNow,
                    RequesterId = userAskingId,
                    TargetId = userTargetId,
                };

                _context.UserFollowerRequest.Add(newRequest);
                await _context.SaveChangesAsync();

                return ErrorResponse.Ok("Sucess, requested.");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to request follow. UserId: {userAskingId} Targer: {userTargetId} Method: {nameof(RequestFollow)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<ErrorResponse> FollowUser(string userId, string userToFollowId)
        {
            try
            {
                var followed = await _context.UserFollower.AnyAsync(a=>a.FollowerId == userId && a.FolloweeId == userToFollowId);
                if (followed)
                {
                    return ErrorResponse.Ok("Already followed");
                }

                var userFollow = new UserFollower
                {
                    FollowerId = userId,
                    FolloweeId = userToFollowId
                };

                var user = await _context.AppUser.FirstOrDefaultAsync(a=>a.Id == userId);
                var userToFollow = await _context.AppUser.FirstOrDefaultAsync(a=>a.Id == userToFollowId);
                if(user == null || userToFollow == null)
                {
                    return ErrorResponse.NotFound("Couldnt find user with given id");
                } 

                user.FollowingCount += 1;
                userToFollow.FollowersCount += 1;
                _context.UserFollower.Add(userFollow);

                await _context.SaveChangesAsync();

                return ErrorResponse.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while tryinh to follow user. UserId: {userId} FollowUserId: {userToFollowId} Method: {nameof(FollowUser)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<ErrorResponse> UnFollowUser(string userId, string userToUnfollowId)
        {
            try
            {
                var followed = await _context.UserFollower.FirstOrDefaultAsync(a => a.FollowerId == userId && a.FolloweeId == userToUnfollowId);
                if (followed == null)
                {
                    return ErrorResponse.Ok("Already not following.");
                }

                var user = await _context.AppUser.FirstOrDefaultAsync(a => a.Id == userId);
                var userToUnFollow = await _context.AppUser.FirstOrDefaultAsync(a => a.Id == userToUnfollowId);
                if (user == null || userToUnFollow == null)
                {
                    return ErrorResponse.NotFound("Couldnt find user with given id");
                }

                _context.UserFollower.Remove(followed);
                user.FollowingCount -= 1;
                userToUnFollow.FollowersCount -= 1;
                await _context.SaveChangesAsync();

                return ErrorResponse.Ok();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to unfollow user. UserId: {userId} UnfollowUserId: {userToUnfollowId} Method: {nameof(UnFollowUser)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }
        public async Task<ErrorResponse> BlockUser(string userId, string userToBlockId)
        {
            try
            {
                var userToBlock = await _context.AppUser.FirstOrDefaultAsync( a => a.Id == userToBlockId);
                var user = await _context.AppUser.FirstOrDefaultAsync(a=>a.Id == userId);

                if(userToBlock == null || user == null)
                {
                    return ErrorResponse.NotFound("Couldn't find any user with given id.");
                }

                var followed = await _context.UserFollower.FirstOrDefaultAsync(a => a.FollowerId == userId && a.FolloweeId == userToBlockId);
                if (followed != null)
                {
                    userToBlock.FollowersCount = Math.Max(0, userToBlock.FollowersCount - 1);
                    user.FollowingCount = Math.Max(0, user.FollowingCount - 1);

                    _context.UserFollower.Remove(followed);
                }

                var alreadyBlocked = await _context.UserBlock.AnyAsync(b => b.BlockerId == userId && b.BlockedId == userToBlockId);
                if (alreadyBlocked)
                {
                    return ErrorResponse.AlreadyExists("User is already blocked.");
                }

                var newBlockRecord = new UserBlock()
                {
                    BlockerId = userId,
                    BlockedId = userToBlockId,
                };

                _context.UserBlock.Add(newBlockRecord);
                await _context.SaveChangesAsync();

                return ErrorResponse.Ok();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to block user. UserId: {userId} BlockingUserId: {userToBlockId} Method: {nameof(BlockUser)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }
        public async Task<ErrorResponse> UnBlockUser(string userId, string userToUnblockId)
        {
            try
            {
                var userToUnblock = await _context.AppUser.FirstOrDefaultAsync(a => a.Id == userToUnblockId);
                var user = await _context.AppUser.FirstOrDefaultAsync(a => a.Id == userId);

                if (userToUnblock == null || user == null)
                {

                    return ErrorResponse.NotFound("Couldn't find any user with given id.");
                }

                var record = await _context.UserBlock.FirstOrDefaultAsync(b => b.BlockerId == userId && b.BlockedId == userToUnblockId);
                if(record != null)
                {
                    _context.UserBlock.Remove(record);
                    await _context.SaveChangesAsync();
                }

                return ErrorResponse.Ok();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to unlock user. UserId: {userId} BlockingUserId: {userToUnblockId} Method: {nameof(UnBlockUser)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<ErrorResponse> RemoveFollowRequest(string userId, string userIdToRemoveRequestFrom)
        {
            try
            {
                var request = await _context.UserFollowerRequest.FirstOrDefaultAsync(a => a.RequesterId == userId && a.TargetId == userIdToRemoveRequestFrom);
                if (request == null)
                {
                    return ErrorResponse.NotFound("Follow request for user does not exists.");
                }

                _context.UserFollowerRequest.Remove(request);
                await _context.SaveChangesAsync();

                return ErrorResponse.Ok();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to remove follow request. UserId: {userId} From: {userIdToRemoveRequestFrom} Method: {nameof(RemoveFollowRequest)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<ErrorResponse> RespondToFollowRequest(string userId, RespondToFollowVM model)
        {
            try
            {
                switch (model.Decision) 
                {
                    case FollowRequestDecision.Accept:
                        var acceptResponse = await AcceptFollowRequest(userId, model);
                        return acceptResponse;
                    case FollowRequestDecision.Remove:
                        var declineResponse = await DeclineFollowRequest(userId, model);
                        return declineResponse;
                }

                _logger.LogError($"Failed while trying to respond to user request. UserId: {userId} Method: {nameof(RespondToFollowRequest)}");
                return ErrorResponse.Failed($"Failed while trying to respond to user request. Check {nameof(RespondToFollowVM)}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to respond to user request. UserId: {userId} Method: {nameof(RespondToFollowRequest)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<(FriendsLeaderboardVMO data, ErrorResponse error)> GetFriendsLeaderboards(string userId)
        {
            try
            {
                var vmo = new FriendsLeaderboardVMO();

                var user = await _context.AppUser.FirstOrDefaultAsync(a=>a.Id == userId);
                if(user == null)
                {
                    _logger.LogWarning($"User with given id: {userId} not found. Method: {nameof(GetFriendsLeaderboards)}");
                    return (vmo, ErrorResponse.NotFound($"User with id: {userId} not found."));
                }

                var followed = await GetUserFollowerLists(userId, true);
                if (!followed.error.Success)
                {
                    _logger.LogWarning($"Couldn't get followed friends for user. {nameof(GetFriendsLeaderboards)}");
                    return (vmo, followed.error);
                }

                var idsToProcess = followed.data.Followed.Select(f => f.UserId).Distinct().ToList();

                if (!idsToProcess.Contains(userId))
                    idsToProcess.Add(userId);

                var metricTasks = idsToProcess.Select(id => GetFriendsLeaderboardMetricForUser(id, id == userId)).ToList();
                var metricResults = await Task.WhenAll(metricTasks);

                var firstError = metricResults.FirstOrDefault(r => !r.error.Success).error;

                if (firstError != null)
                    return (vmo, firstError);

                foreach (var type in Enum.GetValues<LeaderboardType>().Cast<LeaderboardType>())
                {
                    var combined = new Leaderboard { Type = type };

                    foreach (var (data, _) in metricResults)
                    {
                        var userLb = data.FirstOrDefault(l => l.Type == type);
                        if (userLb == null)
                            continue;

                        combined.All.AddRange(userLb.All);
                        combined.Year.AddRange(userLb.Year);
                        combined.Month.AddRange(userLb.Month);
                        combined.Week.AddRange(userLb.Week);
                    }

                    Action<List<LeaderboardRecord>> sortAndRank = list =>
                    {
                        IEnumerable<LeaderboardRecord> sorted;

                        if (type == LeaderboardType.Running || type == LeaderboardType.Swimming)
                        {
                            sorted = list
                                .Where(r => r.CardioSpecific != null)
                                .OrderByDescending(r => r.CardioSpecific!.SpeedKmh)
                                .ThenByDescending(r => r.CardioSpecific!.DistanceKm)
                                .ThenBy(r => r.CardioSpecific!.Time);
                        }
                        else
                        {
                            sorted = list.OrderByDescending(r => r.Value);
                        }

                        var ranked = sorted
                            .Select((record, idx) =>
                            {
                                record.LeaderboardPosition = idx + 1;
                                return record;
                            }).ToList();

                        list.Clear();
                        list.AddRange(ranked);
                    };

                    sortAndRank(combined.All);
                    sortAndRank(combined.Year);
                    sortAndRank(combined.Month);
                    sortAndRank(combined.Week);

                    vmo.Leaderboards.Add(combined);
                }

                return (vmo, ErrorResponse.Ok());
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get friends leaderboards UserId: {userId} Method: {nameof(GetFriendsLeaderboards)}");
                return (new FriendsLeaderboardVMO(), ErrorResponse.Internal(ex.Message));
            }
        }

        private async Task<(List<Leaderboard> data, ErrorResponse error)> GetFriendsLeaderboardMetricForUser(string userId, bool isOwn = false)
        {
            try
            {
                using var ctx = _contextFactory.CreateDbContext();

                var vmo = new List<Leaderboard>
                {
                    new Leaderboard { Type = LeaderboardType.Calories },
                    new Leaderboard { Type = LeaderboardType.Steps    },
                    new Leaderboard { Type = LeaderboardType.Activity },
                    new Leaderboard { Type = LeaderboardType.Running  },
                    new Leaderboard { Type = LeaderboardType.Swimming },
                    new Leaderboard { Type = LeaderboardType.Benchpress },
                    new Leaderboard { Type = LeaderboardType.Deadlift },
                    new Leaderboard { Type = LeaderboardType.Squats },
                };

                var user = await ctx.AppUser.Where(a => a.Id == userId).Select(a => new { a.Name, Pfp = a.Pfp, a.Id }).FirstOrDefaultAsync();
                if (user == null)
                {
                    _logger.LogWarning($"User with given id not found. UserId: {userId} Method: {nameof(GetFriendsLeaderboardMetricForUser)}");
                    return (vmo, ErrorResponse.NotFound("User with given id not found."));
                }

                var userData = new LeaderboardUserData { Name = user.Name ?? "user", Pfp = user.Pfp ?? String.Empty, UserId = user.Id ?? String.Empty };

                var statsDoc = await _userStatisticsDocument.Find(s => s.UserId == userId) .FirstOrDefaultAsync();
                var dailyDoc = await _cardioDocument.Find(d => d.UserId == userId).FirstOrDefaultAsync();
                var historyDoc = await _cardioHistoryDocument.Find(h => h.UserId == userId).FirstOrDefaultAsync();
                var dailyDocGym = await _trainingCollection.Find(a=>a.UserId == userId).FirstOrDefaultAsync();
                var historyDocGym = await _exerciseHistory.Find(a => a.UserId == userId).FirstOrDefaultAsync();


                var now = DateTime.UtcNow;
                var yearAgo = now.AddYears(-1);
                var monthAgo = now.AddMonths(-1);
                var weekAgo = now.AddDays(-7);

                double SumStats(StatisticType type, DateTime since)
                    => statsDoc?.UserStatisticGroups
                                .FirstOrDefault(g => g.Type == type)?
                                .Records
                                .Where(r => r.Date >= since)
                                .Sum(r => r.Value) ?? 0;

                var metricMap = new[]
                {
                    (
                        Type:     LeaderboardType.Calories,
                        AllTime:  statsDoc?.TotalCaloriesCounter ?? 0,
                        StatType: StatisticType.CaloriesBurnt
                    ),
                    (
                        Type:     LeaderboardType.Steps,
                        AllTime:  statsDoc?.TotalStepsCounter ?? 0,
                        StatType: StatisticType.StepsTaken
                    ),
                    (
                        Type:     LeaderboardType.Activity,
                        AllTime:  statsDoc?.TotalSessionsCounter ?? 0,
                        StatType: StatisticType.ActvSessionsCount
                    )
                };

                foreach (var (Type, AllTime, StatType) in metricMap)
                {
                    var lb = vmo.First(l => l.Type == Type);

                    lb.All.Add(new LeaderboardRecord { UserData = userData, Value = AllTime });
                    lb.Year.Add(new LeaderboardRecord { UserData = userData, Value = SumStats(StatType, yearAgo) });
                    lb.Month.Add(new LeaderboardRecord { UserData = userData, Value = SumStats(StatType, monthAgo) });
                    lb.Week.Add(new LeaderboardRecord { UserData = userData, Value = SumStats(StatType, weekAgo) });
                }

                var cardioSessions = new List<(LeaderboardType Type, DateTime Date, TimeSpan Duration, double DistanceKm, double SpeedKmh)>();

                if (dailyDoc != null)
                {
                    foreach (var day in dailyDoc.Trainings)
                    {
                        foreach (var ex in day.Exercises
                                              .Where(ex =>
                                                  ex.ExerciseVisilibity == ExerciseVisilibity.Public &&
                                                  (ex.ActivityType == ActivityType.Running ||
                                                   ex.ActivityType == ActivityType.Swimming)))
                        {
                            cardioSessions.Add((
                                Type: ex.ActivityType == ActivityType.Running ? LeaderboardType.Running : LeaderboardType.Swimming,
                                Date: day.Date,
                                Duration: ex.Duration,
                                DistanceKm: ex.DistanceMeters / 1000,
                                SpeedKmh: ex.SpeedKmH
                            ));
                        }
                    }
                }

                if (historyDoc != null)
                {
                    foreach (var group in historyDoc.Exercises)
                    {
                        if (group.ActivityType != ActivityType.Running &&
                            group.ActivityType != ActivityType.Swimming)
                            continue;

                        foreach (var data in group.ExercisesData)
                        {
                            cardioSessions.Add((
                                Type: group.ActivityType == ActivityType.Running ? LeaderboardType.Running : LeaderboardType.Swimming,
                                Date: data.Date,
                                Duration: data.Duration,
                                DistanceKm: data.DistanceMeters / 1000,
                                SpeedKmh: data.SpeedKmH
                            ));
                        }
                    }
                }

                foreach (var cardioType in new[] { LeaderboardType.Running, LeaderboardType.Swimming })
                {
                    var lb = vmo.First(l => l.Type == cardioType);

                    LeaderboardRecord? BestIn(DateTime? since)
                    {
                        var candidates = cardioSessions.Where(s => s.Type == cardioType &&(!since.HasValue || s.Date >= since.Value));

                        var best = candidates.OrderByDescending(s => s.SpeedKmh).FirstOrDefault();

                        if (best == default) return null;

                        return new LeaderboardRecord
                        {
                            UserData = userData,
                            Value = best.SpeedKmh,
                            CardioSpecific = new LeaderboardCardioData
                            {
                                SpeedKmh = best.SpeedKmh,
                                DistanceKm = best.DistanceKm,
                                Time = best.Duration,
                                ExerciseDate = best.Date
                            }
                        };
                    }

                    BestIn(null)?.Let(r => lb.All.Add(r));
                    BestIn(yearAgo)?.Let(r => lb.Year.Add(r));
                    BestIn(monthAgo)?.Let(r => lb.Month.Add(r));
                    BestIn(weekAgo)?.Let(r => lb.Week.Add(r));
                }

                var liftSessions = new List<(LeaderboardType Type, DateTime Date, double WeightKg, int Reps)>();
                if (dailyDocGym?.Trainings != null)
                    foreach (var day in dailyDocGym.Trainings)
                        foreach (var ex in day.Exercises.Where(e => (e.Name.Contains("Benchpress") || e.Name.Contains("Deadlift") || e.Name.Contains("Squat"))))
                        {
                            var topSeries = ex.Series.OrderByDescending(s => s.WeightKg).FirstOrDefault();
                            if (topSeries == null) continue;
                            var type = ex.Name.Contains("Benchpress") ? LeaderboardType.Benchpress
                                     : ex.Name.Contains("Deadlift") ? LeaderboardType.Deadlift
                                     : LeaderboardType.Squats;
                            liftSessions.Add((Type: type, Date: day.Date, WeightKg: topSeries.WeightKg, Reps: topSeries.Repetitions));
                        }
                if (historyDocGym?.ExerciseHistoryLists != null)
                    foreach (var list in historyDocGym.ExerciseHistoryLists)
                    {
                        var type = list.ExerciseName.Contains("Benchpress") ? LeaderboardType.Benchpress
                                 : list.ExerciseName.Contains("Deadlift") ? LeaderboardType.Deadlift
                                 : list.ExerciseName.Contains("Squat") ? LeaderboardType.Squats
                                 : (LeaderboardType?)null;
                        if (type == null) continue;
                        foreach (var entry in list.ExerciseData)
                        {
                            var topSeries = entry.Series.OrderByDescending(s => s.WeightKg).FirstOrDefault();
                            if (topSeries == null) continue;
                            liftSessions.Add((Type: type.Value, Date: entry.Date, WeightKg: topSeries.WeightKg, Reps: topSeries.Repetitions));
                        }
                    }

                foreach (var liftType in new[] { LeaderboardType.Benchpress, LeaderboardType.Deadlift, LeaderboardType.Squats })
                {
                    var lb = vmo.First(l => l.Type == liftType);
                    LeaderboardRecord? BestLift(DateTime? since)
                    {
                        var best = liftSessions
                            .Where(s => s.Type == liftType && (!since.HasValue || s.Date >= since.Value))
                            .OrderByDescending(s => s.WeightKg)
                            .ThenByDescending(s => s.Reps)
                            .FirstOrDefault();

                        if (best == default) return null;

                        return new LeaderboardRecord
                        {
                            UserData = userData,
                            Value = best.WeightKg,
                            GymSpecific = new LeaderboardGymData
                            {
                                WeightKg = best.WeightKg,
                                WeightLbs = Math.Round(best.WeightKg * 2.20462, 2),
                                Repetitions = best.Reps
                            }
                        };
                    }

                    BestLift(null)?.Let(r => lb.All.Add(r));
                    BestLift(yearAgo)?.Let(r => lb.Year.Add(r));
                    BestLift(monthAgo)?.Let(r => lb.Month.Add(r));
                    BestLift(weekAgo)?.Let(r => lb.Week.Add(r));
                }

                return (vmo, ErrorResponse.Ok());

            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get leaderboard metrics data for user. UserId: {userId} Method: {nameof(GetFriendsLeaderboardMetricForUser)}");
                return (new List<Leaderboard>(), ErrorResponse.Internal(ex.Message));
            }
        }

        private async Task<ErrorResponse> AcceptFollowRequest(string userId, RespondToFollowVM model)
        {
            try
            {
                var request = await _context.UserFollowerRequest.FirstOrDefaultAsync(a => a.Id == model.RequestId && a.TargetId == userId && a.RequesterId == model.RequestingUserId);
                if (request == null)
                {
                    return ErrorResponse.NotFound("Request does not exists.");
                }

                _context.UserFollowerRequest.Remove(request);
                var followResponse = await FollowUser(model.RequestingUserId, userId);
                if (!followResponse.Success)
                {
                    return followResponse;
                }

                await _context.SaveChangesAsync();
                return ErrorResponse.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to respond to accept user request. UserId: {userId} Method: {nameof(AcceptFollowRequest)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        private async Task<ErrorResponse> DeclineFollowRequest(string userId, RespondToFollowVM model)
        {
            try
            {
                var request = await _context.UserFollowerRequest.FirstOrDefaultAsync(a => a.Id == model.RequestId && a.TargetId == userId && a.RequesterId == model.RequestingUserId);
                if (request == null)
                {
                    return ErrorResponse.NotFound("Request does not exists.");
                }

                _context.UserFollowerRequest.Remove(request);
                await _context.SaveChangesAsync();

                return ErrorResponse.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to respond to decline user request. UserId: {userId} Method: {nameof(DeclineFollowRequest)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<(UserFollowersVMO data, ErrorResponse error)> GetUserFollowerLists(string userId, bool onlyFollowed, string askingUserId = null)
        {
            try
            {
                var vmo = new UserFollowersVMO();

                var user = await _context.AppUser.Include(u => u.Followers).ThenInclude(f => f.Follower)
                                                    .Include(u => u.Following).ThenInclude(f => f.Followee)
                                                        .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    return (new UserFollowersVMO(), ErrorResponse.NotFound("User not found."));
                }

                List<string> askingUserFolloweeIds = new List<string>();
                var isSelfRequest = string.IsNullOrEmpty(askingUserId) || askingUserId == userId;
                if (!isSelfRequest)
                {
                    askingUserFolloweeIds = await _context.UserFollower
                        .Where(f => f.FollowerId == askingUserId)
                        .Select(f => f.FolloweeId)
                        .ToListAsync();
                }

                List<string> askingUserIdRequestedFollowsIds = new List<string>();
                askingUserIdRequestedFollowsIds = await _context.UserFollowerRequest.Where(a => a.RequesterId == askingUserId).Select(a => a.TargetId).ToListAsync();

                if (!onlyFollowed)
                {
                    vmo.Followers = user.Followers.Select(f => new UserFollowersList
                    {
                        UserId = f.Follower.Id,
                        Name = f.Follower.Name??"User",
                        Pfp = f.Follower.Pfp,
                        IsPrivate = f.Follower.IsProfilePrivate,
                        IsFollowed = user.Following.Any(ff => ff.FolloweeId == f.FollowerId),
                        IsRequested = askingUserIdRequestedFollowsIds.Contains(f.FollowerId),
                        FollowedByAskingUser = isSelfRequest ? user.Following.Any(ff => ff.FolloweeId == f.FollowerId) : askingUserFolloweeIds.Contains(f.FollowerId)
                    }).ToList();
                }

                vmo.Followed = user.Following.Select(f => new UserFollowersList
                {
                    UserId = f.Followee.Id,
                    Name = f.Followee.Name??"User",
                    Pfp = f.Followee.Pfp,
                    IsPrivate= f.Followee.IsProfilePrivate,
                    IsFollowed = true,
                    IsRequested = askingUserIdRequestedFollowsIds.Contains(f.FolloweeId),
                    FollowedByAskingUser = isSelfRequest ? true : askingUserFolloweeIds.Contains(f.FolloweeId)
                }).ToList();

                return (vmo, ErrorResponse.Ok());
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get user follow lists. UserId: {userId} Method: {nameof(GetUserFollowerLists)}");
                return (new UserFollowersVMO(), ErrorResponse.Internal(ex.Message));
            }
        }

        public async Task<(BlockListVMO data, ErrorResponse error)> GetUserBlockList(string userId)
        {
            try
            {
                var vmo = new BlockListVMO();
                var user = await _context.AppUser.Include(u => u.BlockedUsers).ThenInclude(a=>a.Blocked).FirstOrDefaultAsync(a=>a.Id == userId);
                if (user == null)
                {
                    _logger.LogWarning($"Trying to acess non existing user Method: {nameof(GetUserBlockList)}");
                    return (vmo, ErrorResponse.NotFound($"User with id: {userId} Not found."));
                }

                vmo.BlockList = user.BlockedUsers.Select(a => new BlockList
                {
                    Name = a.Blocked.Name??"User",
                    Pfp = a.Blocked.Pfp,       
                    UserId = a.Blocked.Id,
                }).ToList();

                return (vmo, ErrorResponse.Ok());
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get user block list. UserId: {userId} Method: {nameof(GetUserBlockList)}");
                return (new BlockListVMO(), ErrorResponse.Internal(ex.Message));
            }
        }

        public async Task<(UserSearchVMO data, ErrorResponse error)> SearchForUsers(string userId, string query, int limit = 10)
        {
            try
            {
                var vmo = new UserSearchVMO();

                var blockedUserIds = await _context.UserBlock.Where(b => b.BlockerId == userId || b.BlockedId == userId)
                    .Select(b => b.BlockerId == userId ? b.BlockedId : b.BlockerId).Distinct().ToListAsync();

                blockedUserIds.Add(userId);

                var users = await _context.AppUser
                    .Where(u => !blockedUserIds.Contains(u.Id) && u.Name.ToLower().StartsWith(query)).OrderBy(u => u.Name).Take(limit)
                    .Select(u => new UserSearch
                    {
                        UserId = u.Id,
                        Name = u.Name??"User",
                        Pfp = u.Pfp
                    }).ToListAsync();

                vmo.Users = users;

                return (vmo, ErrorResponse.Ok());
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to perform user search. UserRequestingId: {userId} Query: {query} Method: {nameof(SearchForUsers)}");
                return (new UserSearchVMO(), ErrorResponse.Internal(ex.Message));
            }
        }

        public async Task<(UserProfileDataVMO data, ErrorResponse error)> GetUserProfileData(string userId, string askingUserId, bool full = true)
        {
            try
            {
                var vmo = new UserProfileDataVMO();

                bool isFollowed = false;
                bool isRequested = false;
                bool isOwn = false;

                if(userId == askingUserId)
                {
                    isOwn = true;
                }

                if(userId != askingUserId)
                {
                    isFollowed = await CheckIfUserFollowUser(askingUserId, userId);
                }

                if(userId != askingUserId && !isFollowed)
                {
                    var existingRequest = await _context.UserFollowerRequest.FirstOrDefaultAsync(a => a.RequesterId == askingUserId && a.TargetId == userId);
                    isRequested = (existingRequest != null);
                }

                var generalUserProfileData = await GetGeneralProfileData(userId);
                if (!generalUserProfileData.error.Success)
                {
                    return (vmo, generalUserProfileData.error);
                }

                vmo.GeneralProfileData = generalUserProfileData.data;
                vmo.GeneralProfileData.UserId = userId;
                vmo.GeneralProfileData.IsFollowed = isFollowed;
                vmo.GeneralProfileData.IsRequested = isRequested;
                vmo.GeneralProfileData.IsOwn = isOwn;

                if (!full)
                {
                    return (vmo, ErrorResponse.Ok());
                }


                var earnedBadgesTask = GetUserEarnedBadges(userId);
                var recentCardioTask = GetUserRecentCardioActivity(userId);
                var bestCardioTask = GetUserBestCardioActivities(userId);
                var recentLiftsTask = GetUserRecentLiftActivities(userId);
                var bestLiftsTask = GetUserBestLifts(userId);
                var statisticsTask = GetUserStatistics(userId);
                var cardioStatsTask = GetCardioTrainingStatistics(userId);

                await Task.WhenAll(earnedBadgesTask, recentCardioTask, recentLiftsTask, bestLiftsTask, statisticsTask, cardioStatsTask);

                if (!earnedBadgesTask.Result.error.Success) return (vmo, earnedBadgesTask.Result.error);
                if (!recentCardioTask.Result.error.Success) return (vmo, recentCardioTask.Result.error);
                if(!bestCardioTask.Result.error.Success) return (vmo,  bestCardioTask.Result.error);
                if (!recentLiftsTask.Result.error.Success) return (vmo, recentLiftsTask.Result.error);
                if (!bestLiftsTask.Result.error.Success) return (vmo, bestLiftsTask.Result.error);
                if (!statisticsTask.Result.error.Success) return (vmo, statisticsTask.Result.error);
                if (!cardioStatsTask.Result.error.Success) return (vmo, cardioStatsTask.Result.error);

                vmo.PrivateProfileInformation = new PrivateProfileInformation
                {
                    EarnedBadges = earnedBadgesTask.Result.data,
                    RecentCardioActivities = recentCardioTask.Result.data,
                    BestCardioActivities = bestCardioTask.Result.data,
                    RecentLiftActivities = recentLiftsTask.Result.data,
                    BestLifts = bestLiftsTask.Result.data,
                    Statistics = statisticsTask.Result.data,
                    CardioStatistics = cardioStatsTask.Result.data
                };

                return (vmo, ErrorResponse.Ok());
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get user profile data. UserId: {userId} Method: {nameof(GetUserProfileData)}");
                return (new UserProfileDataVMO(), ErrorResponse.Internal(ex.Message));
            }
        }

        private async Task<(GeneralProfileData data, ErrorResponse error)> GetGeneralProfileData(string userId)
        {
            try
            {
                var vmo = new GeneralProfileData();
                using var context = _contextFactory.CreateDbContext();
                
                var user = await context.AppUser.FirstOrDefaultAsync(a=>a.Id == userId);
                if(user == null)
                {
                    return (vmo, ErrorResponse.NotFound("User with given id not found."));
                }

                vmo.Pfp = user.Pfp;
                vmo.Name = user.Name ?? "User";
                vmo.Desc = user.Desc ?? string.Empty;
                vmo.FollowedCounter = user.FollowingCount;
                vmo.FollowersCounter = user.FollowersCount;
                vmo.IsPrivate = user.IsProfilePrivate;

                return (vmo, ErrorResponse.Ok());
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get general profile data for user. UserId: {userId} Method: {nameof(GetGeneralProfileData)}");
                return (new GeneralProfileData(), ErrorResponse.Internal(ex.Message));
            }
        }

        private async Task<(List<EarnedBadges> data, ErrorResponse error)> GetUserEarnedBadges(string userId)
        {
            try
            {
                var vmo = new List<EarnedBadges>();
                using var context = _contextFactory.CreateDbContext();

                var user = await context.AppUser.Include(a => a.UserBadges).ThenInclude(ub => ub.Challange).Include(a => a.Achievments).FirstOrDefaultAsync(a => a.Id == userId);
                if (user == null)
                {
                    return (vmo, ErrorResponse.NotFound("User with given id not found."));
                }

                if (user.UserBadges != null)
                {
                    var challengeProgress = await context.ActiveChallange.Where(ac => ac.UserId == userId).ToDictionaryAsync(ac => ac.ChallengeId, ac => ac.CurrentProgress);

                    foreach (var badge in user.UserBadges)
                    {
                        challengeProgress.TryGetValue(badge.ChallangeId, out var currentProgress);

                        vmo.Add(new EarnedBadges
                        {
                            BadgeType = BadgeType.Challange,
                            Name = badge.Challange.Name,
                            Img = badge.Challange.Badge,
                            Threshold = badge.Challange.GoalValue,
                            CurrentTotalProgress = currentProgress,
                            ChallengeGoalType = badge.Challange.GoalType,
                        });
                    }
                }

                if (user.Achievments != null)
                {
                    var achievementIds = user.Achievments.Select(a => a.Id).ToList();
                    var achievementCounters = await context.AchievementCounters.Where(ac => ac.UserId == userId && achievementIds.Contains(ac.AchievmentId)).ToDictionaryAsync(ac => ac.AchievmentId, ac => ac.Counter);

                    foreach (var ach in user.Achievments)
                    {
                        achievementCounters.TryGetValue(ach.Id, out var counter);

                        vmo.Add(new EarnedBadges
                        {
                            BadgeType = BadgeType.Achievment,
                            Name = ach.Name,
                            Img = ach.Img,
                            Threshold = ach.Threshold,
                            CurrentTotalProgress = counter,
                            ChallengeGoalType = ChallengeGoalType.None,
                        });
                    }
                }

                return (vmo, ErrorResponse.Ok());
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get user earned badges and achievments data. UserId: {userId} Method: {nameof(GetUserEarnedBadges)}");
                return (new List<EarnedBadges>(), ErrorResponse.Internal(ex.Message));
            }
        }

        private async Task<(List<RecentCardioActivity> data, ErrorResponse error)> GetUserRecentCardioActivity(string userId, int limit = 15)
        {
            try
            {
                var vmo = new List<RecentCardioActivity>();

                var userCardioDoc = await _cardioDocument.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if (userCardioDoc != null)
                {
                    var allSessions = userCardioDoc.Trainings.OrderByDescending(t => t.Date).SelectMany(t => t.Exercises.Select(e => new { PlanDate = t.Date, Exercise = e }))
                                                             .OrderByDescending(e => e.PlanDate).ThenByDescending(e => e.Exercise.PublicId).Where(e => e.Exercise.ExerciseVisilibity == ExerciseVisilibity.Public);

                    foreach (var item in allSessions)
                    {
                        if (limit > 0)
                        {
                            var session = item.Exercise;
                            vmo.Add(new RecentCardioActivity
                            {
                                Date = item.PlanDate,
                                Name = session.Name,
                                Desc = session.Desc,
                                Duration = session.Duration,
                                DistanceMeters = session.DistanceMeters,
                                SpeedKmH = session.SpeedKmH,
                                AvgHeartRate = session.AvgHeartRate,
                                CaloriesBurnt = session.CaloriesBurnt,
                                FeelingPercentage = session.FeelingPercentage,
                                ExerciseFeeling = session.ExerciseFeeling,
                                Route = session.Route,
                                ActivityType = session.ActivityType,
                                HeartRateInTime = session.HeartRateInTime ?? new List<HeartRateInTime>(),
                                SpeedInTime = session.SpeedInTime ?? new List<SpeedInTime>(),
                            });

                            limit--;
                        }
                    }
                }

                if (limit > 0)
                {
                    var userCardioHistoryDoc = await _cardioDailyHistoryDocument.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                    if (userCardioHistoryDoc != null)
                    {
                        var historySessions = userCardioHistoryDoc.Trainings.OrderByDescending(t => t.Date).Where(t => t.CardioTraining.ExerciseVisilibity == ExerciseVisilibity.Public)
                            .Select(t => new { PlanDate = t.Date, Exercise = t.CardioTraining }).OrderByDescending(e => e.PlanDate).ThenByDescending(e => e.Exercise.PublicId).Take(limit);

                        foreach (var item in historySessions)
                        {
                            vmo.Add(new RecentCardioActivity
                            {
                                Date = item.PlanDate,
                                Name = item.Exercise.Name,
                                Desc = item.Exercise.Desc,
                                Duration = item.Exercise.Duration,
                                DistanceMeters = item.Exercise.DistanceMeters,
                                SpeedKmH = item.Exercise.SpeedKmH,
                                AvgHeartRate = item.Exercise.AvgHeartRate,
                                CaloriesBurnt = item.Exercise.CaloriesBurnt,
                                FeelingPercentage = item.Exercise.FeelingPercentage,
                                ExerciseFeeling = item.Exercise.ExerciseFeeling,
                                Route = item.Exercise.Route,
                                ActivityType = item.Exercise.ActivityType,
                                HeartRateInTime = item.Exercise.HeartRateInTime ?? new List<HeartRateInTime>(),
                                SpeedInTime = item.Exercise.SpeedInTime ?? new List<SpeedInTime>(),
                            });
                        }

                    }
                }

                return (vmo, ErrorResponse.Ok());
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get recent cardio activity for user. UserId: {userId} Method: {nameof(GetUserRecentCardioActivity)}");
                return (new List<RecentCardioActivity>(), ErrorResponse.Internal(ex.Message));
            }
        }

        private async Task<(List<RecentCardioActivity> data, ErrorResponse error)> GetUserBestCardioActivities(string userId)
        {
            try
            {
                var bestActivities = new Dictionary<ActivityType, (DateTime Date, CardioTraining Training, double Score)>();

                var userCardioDoc = await _cardioDocument.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if (userCardioDoc != null)
                {
                    foreach (var training in userCardioDoc.Trainings)
                    {
                        foreach (var exercise in training.Exercises)
                        {
                            if (exercise.ExerciseVisilibity != ExerciseVisilibity.Public)
                                continue;

                            double score = (exercise.DistanceMeters * 0.5) + (exercise.SpeedKmH * 10) + (exercise.Duration.TotalMinutes * 1.5);

                            if (!bestActivities.TryGetValue(exercise.ActivityType, out var currentBest) || score > currentBest.Score)
                            {
                                bestActivities[exercise.ActivityType] = (training.Date, exercise, score);
                            }
                        }
                    }
                }

                var userCardioHistoryDoc = await _cardioDailyHistoryDocument.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                if (userCardioHistoryDoc != null)
                {
                    foreach (var training in userCardioHistoryDoc.Trainings)
                    {
                        var ex = training.CardioTraining;
                        if (ex.ExerciseVisilibity != ExerciseVisilibity.Public)
                            continue;

                        double score = (ex.DistanceMeters * 0.5) + (ex.SpeedKmH * 10) + (ex.Duration.TotalMinutes * 1.5);

                        if (!bestActivities.TryGetValue(ex.ActivityType, out var currentBest) || score > currentBest.Score)
                        {
                            bestActivities[ex.ActivityType] = (training.Date, ex, score);
                        }
                    }
                }

                var vmo = bestActivities.Values.Select(item => new RecentCardioActivity
                {
                    Date = item.Date,
                    Name = item.Training.Name,
                    Desc = item.Training.Desc,
                    Duration = item.Training.Duration,
                    DistanceMeters = item.Training.DistanceMeters,
                    SpeedKmH = item.Training.SpeedKmH,
                    AvgHeartRate = item.Training.AvgHeartRate,
                    CaloriesBurnt = item.Training.CaloriesBurnt,
                    FeelingPercentage = item.Training.FeelingPercentage,
                    ExerciseFeeling = item.Training.ExerciseFeeling,
                    Route = item.Training.Route,
                    ActivityType = item.Training.ActivityType,
                    HeartRateInTime = item.Training.HeartRateInTime ?? new List<HeartRateInTime>(),
                    SpeedInTime = item.Training.SpeedInTime ?? new List<SpeedInTime>(),
                }).ToList();

                return (vmo, ErrorResponse.Ok());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get best cardio activity for user. UserId: {userId} Method: {nameof(GetUserBestCardioActivities)}");
                return (new List<RecentCardioActivity>(), ErrorResponse.Internal(ex.Message));
            }
        }

        private async Task<(List<LiftData> data, ErrorResponse error)> GetUserRecentLiftActivities(string userId, int limit = 10)
        {
            try
            {
                var vmo = new List<LiftData>();

                var userTrainingDoc = await _trainingCollection.Find(a=>a.UserId == userId).FirstOrDefaultAsync();
                if(userTrainingDoc != null)
                {
                    var allSessions = userTrainingDoc.Trainings.OrderByDescending(t => t.Date).SelectMany(t => t.Exercises.Select(e => new { PlanDate = t.Date, Exercise = e }))
                                                             .OrderByDescending(e => e.PlanDate).ThenByDescending(e => e.Exercise.PublicId);

                    foreach (var item in allSessions)
                    {
                        if (limit > 0)
                        {
                            var exerciseRecord = new LiftData()
                            {
                                Date = item.PlanDate,
                                Name = item.Exercise.Name,
                                Series = item.Exercise.Series,
                            };
                            vmo.Add(exerciseRecord);

                            limit--;
                        }
                    }
                }

                if(limit > 0)
                {
                    var userTrainingHistoryDoc = await _trainingHistoryCollection.Find(a=>a.UserId ==userId).FirstOrDefaultAsync();
                    if(userTrainingHistoryDoc != null)
                    {
                        var historySessions = userTrainingHistoryDoc.DailyTrainingPlans.OrderByDescending(t => t.Date).SelectMany(t => t.Exercises.Select(e => new { PlanDate = t.Date, Exercise = e })).OrderByDescending(e => e.PlanDate).ThenByDescending(e => e.Exercise.PublicId).Take(limit);

                        foreach (var ex in historySessions)
                        {
                            var exerciseRecord = new LiftData()
                            {
                                Date = ex.PlanDate,
                                Name = ex.Exercise.Name,
                                Series = ex.Exercise.Series,
                            };

                            vmo.Add(exerciseRecord);
                        }
                    }
                }

                return (vmo, ErrorResponse.Ok());
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get user recent lift activities. UserId: {userId} Method: {nameof(GetUserRecentLiftActivities)}");
                return (new List<LiftData>(), ErrorResponse.Internal(ex.Message));
            }
        }

        private async Task<(List<BestLiftData> data, ErrorResponse error)> GetUserBestLifts(string userId)
        {
            try
            {
                var bestLifts = new Dictionary<string, BestLiftData>();

                var userTrainingDoc = await _trainingCollection.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                var userTrainingHistoryDoc = await _trainingHistoryCollection.Find(a => a.UserId == userId).FirstOrDefaultAsync();
                var userExerciseHistoryDoc = await _exerciseHistory.Find(a => a.UserId == userId).FirstOrDefaultAsync();

                var gymLifts = new[] { "Benchpress", "Deadlift", "Squat" };

                if (userTrainingDoc?.Trainings != null)
                {
                    foreach (var training in userTrainingDoc.Trainings)
                    {
                        foreach (var ex in training.Exercises)
                        {
                            if (!gymLifts.Any(l => ex.Name.Contains(l, StringComparison.OrdinalIgnoreCase)))
                                continue;

                            var bestSeries = ex.Series?
                                .OrderByDescending(s => s.WeightKg)
                                .ThenByDescending(s => s.Repetitions)
                                .FirstOrDefault();

                            if (bestSeries == null) continue;

                            UpdateBestLift(bestLifts, ex.Name, bestSeries.WeightKg, bestSeries.Repetitions);
                        }
                    }
                }

                if (userExerciseHistoryDoc?.ExerciseHistoryLists != null)
                {
                    foreach (var list in userExerciseHistoryDoc.ExerciseHistoryLists)
                    {
                        if (!gymLifts.Any(l => list.ExerciseName.Contains(l, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        foreach (var entry in list.ExerciseData)
                        {
                            var bestSeries = entry.Series?
                                .OrderByDescending(s => s.WeightKg)
                                .ThenByDescending(s => s.Repetitions)
                                .FirstOrDefault();

                            if (bestSeries == null) continue;

                            UpdateBestLift(bestLifts, list.ExerciseName, bestSeries.WeightKg, bestSeries.Repetitions);
                        }
                    }
                }

                var otherExercises = new List<DailyExercise>();

                if (userTrainingDoc?.Trainings != null)
                {
                    otherExercises.AddRange(
                        userTrainingDoc.Trainings
                            .SelectMany(t => t.Exercises)
                            .Where(e => e != null && !string.IsNullOrWhiteSpace(e.Name) && !gymLifts.Any(l => e.Name.Contains(l, StringComparison.OrdinalIgnoreCase)))
                    );
                }

                if (userTrainingHistoryDoc?.DailyTrainingPlans != null)
                {
                    otherExercises.AddRange(
                        userTrainingHistoryDoc.DailyTrainingPlans
                            .SelectMany(p => p.Exercises)
                            .Where(e => e != null && !string.IsNullOrWhiteSpace(e.Name) && !gymLifts.Any(l => e.Name.Contains(l, StringComparison.OrdinalIgnoreCase)))
                    );

                }

                foreach (var ex in otherExercises)
                {
                    if (ex.Series == null || ex.Series.Count == 0)
                        continue;

                    var bestSeries = ex.Series
                        .OrderByDescending(s => s.WeightKg)
                        .ThenByDescending(s => s.Repetitions)
                        .FirstOrDefault();

                    if (bestSeries == null) continue;

                    UpdateBestLift(bestLifts, ex.Name, bestSeries.WeightKg, bestSeries.Repetitions);
                }

                return (bestLifts.Values.ToList(), ErrorResponse.Ok());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get user best lifts data. UserId: {userId} Method: {nameof(GetUserBestLifts)}");
                return (new List<BestLiftData>(), ErrorResponse.Internal(ex.Message));
            }
        }

        private void UpdateBestLift(Dictionary<string, BestLiftData> bestLifts, string name, double weightKg, int reps)
        {
            if (!bestLifts.TryGetValue(name, out var existing))
            {
                bestLifts[name] = new BestLiftData
                {
                    Name = name,
                    WeightKg = weightKg,
                    Repetitions = reps
                };
            }
            else
            {
                if (weightKg > existing.WeightKg ||
                    (weightKg == existing.WeightKg && reps > existing.Repetitions))
                {
                    bestLifts[name] = new BestLiftData
                    {
                        Name = name,
                        WeightKg = weightKg,
                        Repetitions = reps
                    };
                }
            }
        }



        private async Task<(Statistics data, ErrorResponse error)> GetUserStatistics(string userId)
        {
            try
            {
                var vmo = new Statistics();

                var userStatisticsCollection = await _userStatisticsDocument.Find(a=>a.UserId == userId).FirstOrDefaultAsync();
                if(userStatisticsCollection == null)
                {
                    _logger.LogWarning($"User statistics document not found. Creating. UserId: {userId}");
                    var newDoc = await _helperService.CreateMissingDoc(userId, _userStatisticsDocument);
                    if (newDoc == null)
                    {
                        _logger.LogCritical($"Unable to create new statistics document for user. UserId: {userId} Method: {nameof(GetUserStatistics)}");
                        return (vmo, ErrorResponse.NotFound("Couldnt find statistics for user."));
                    }

                    userStatisticsCollection = newDoc;
                }

                var now = DateTime.UtcNow;
                var weekStart = now.AddDays(-7);
                var yearStart = new DateTime(now.Year, 1, 1);

                foreach (var group in userStatisticsCollection.UserStatisticGroups)
                {
                    foreach (var record in group.Records)
                    {
                        if (record.Date >= weekStart)
                            AddToPeriod(vmo.Weekly, group.Type, record);

                        if (record.Date >= yearStart)
                            AddToPeriod(vmo.YearToDay, group.Type, record);
                    }
                }

                vmo.AllTime.TotalDistanceKm = userStatisticsCollection.TotalDistanceCounter;
                vmo.AllTime.Steps = userStatisticsCollection.TotalStepsCounter;
                vmo.AllTime.CaloriesBurnt = (int)userStatisticsCollection.TotalCaloriesCounter;
                vmo.AllTime.ActivitiesCount = userStatisticsCollection.TotalSessionsCounter;
                vmo.AllTime.TimeSpentOnActivites = userStatisticsCollection.TotalTimeSpend;
                vmo.AllTime.TotalDistanceKm = userStatisticsCollection.TotalDistanceCounter;
                vmo.AllTime.WeightKg = userStatisticsCollection.TotalWeightLiftedCounter;

                return (vmo, ErrorResponse.Ok());
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get user statistics. UserId: {userId} Method: {nameof(GetUserStatistics)}");
                return (new Statistics(), ErrorResponse.Internal(ex.Message));
            }
        }

        private static void AddToPeriod(PeriodStats stats, StatisticType type, UserStatisticRecord record)
        {
            switch (type)
            {
                case StatisticType.CaloriesBurnt:
                    stats.CaloriesBurnt += (int)record.Value;
                    break;
                case StatisticType.WeightLifted:
                    stats.WeightKg += record.Value;
                    break;
                case StatisticType.StepsTaken:
                    stats.Steps += (int)record.Value;
                    break;
                case StatisticType.ActvSessionsCount:
                    stats.ActivitiesCount += (int)record.Value;
                    break;
                case StatisticType.TimeSpend:
                    stats.TimeSpentOnActivites += record.TimeValue;
                    break;
                case StatisticType.TotalDistance:
                    stats.TotalDistanceKm += record.Value;
                    break;
            }
        }

        private async Task<(CardioTrainingStatistics data, ErrorResponse error)> GetCardioTrainingStatistics(string userId)
        {
            try
            {
                var vmo = new CardioTrainingStatistics();

                var cardioHistory = await _cardioHistoryDocument.Find(ch => ch.UserId == userId).FirstOrDefaultAsync();
                var historyData = new List<(ActivityType ActivityType, TimeSpan Duration, double DistanceMeters, int CaloriesBurnt)>();

                if (cardioHistory?.Exercises != null)
                {
                    historyData = cardioHistory.Exercises
                        .SelectMany(e => e.ExercisesData.Select(d => (
                            ActivityType: e.ActivityType,
                            Duration: d.Duration,
                            DistanceMeters: d.DistanceMeters,
                            CaloriesBurnt: d.CaloriesBurnt
                        )))
                        .ToList();
                }

                var cardioPlan = await _cardioDocument.Find(ch => ch.UserId == userId).FirstOrDefaultAsync();
                var trainingData = new List<(ActivityType ActivityType, TimeSpan Duration, double DistanceMeters, int CaloriesBurnt)>();

                if (cardioPlan?.Trainings != null)
                {
                    trainingData = cardioPlan.Trainings
                        .SelectMany(day => day.Exercises.Select(e => (
                            ActivityType: e.ActivityType,
                            Duration: e.Duration,
                            DistanceMeters: e.DistanceMeters,
                            CaloriesBurnt: e.CaloriesBurnt
                        )))
                        .ToList();
                }

                var allData = historyData.Concat(trainingData);

                var groupedStats = allData
                    .GroupBy(d => d.ActivityType)
                    .Select(group => new CardioTrainingStatisticsSpec
                    {
                        ActivityType = group.Key,
                        TotalActivities = group.Count(),
                        TotalDistanceKm = group.Sum(x => x.DistanceMeters) / 1000.0,
                        TotalCaloriesBurnt = group.Sum(x => x.CaloriesBurnt),
                        TotalTimeSpent = TimeSpan.FromSeconds(group.Sum(x => x.Duration.TotalSeconds))
                    })
                    .ToList();

                vmo.Activities = groupedStats;

                return (vmo, ErrorResponse.Ok());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get user statistics for spec cardio. UserId: {userId} Method: {nameof(GetCardioTrainingStatistics)}");
                return (new CardioTrainingStatistics(), ErrorResponse.Internal(ex.Message));
            }
        }

        public async Task<(FollowersRequestVMO data, ErrorResponse erro)> GetFollowersRequests(string userId)
        {
            try
            {
                var vmo = new FollowersRequestVMO();
                
                var requests = await _context.UserFollowerRequest.Where(r => r.TargetId == userId && r.IsPending).Include(r => r.Requester).ToListAsync();
                if(requests != null)
                {
                    vmo.Requests = requests.Select(r => new FollowerRequest
                    {
                        UserId = r.RequesterId,
                        Name = r.Requester.Name ?? "User",
                        Pfp = r.Requester.Pfp,
                        DateRequested = r.RequestedAt,
                        RequestId = r.Id,
                    }).ToList();
                }

                return (vmo, ErrorResponse.Ok());
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get user follower request list. UserId: {userId} Method: {nameof(GetFollowersRequests)}");
                return (new FollowersRequestVMO(), ErrorResponse.Internal(ex.Message));
            }
        }

        public async Task<(ErrorResponse error, string profilePcitrue)> GetUserProfilePicture(string userId)
        {
            try
            {
                var user = await _context.AppUser.FirstOrDefaultAsync(a => a.Id == userId);
                if(user == null)
                {
                    return (ErrorResponse.NotFound("User not found."), String.Empty);
                }

                return (ErrorResponse.Ok(), user.Pfp);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get user profile picture. UserId: {userId} Method: {nameof(GetUserProfilePicture)}");
                return (ErrorResponse.Internal(ex.Message), String.Empty);
            }
        }
    }
}