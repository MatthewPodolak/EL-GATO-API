using ElGato_API.Data;
using ElGato_API.Interfaces;
using ElGato_API.Models.User;
using ElGato_API.VMO.Community;
using ElGato_API.VMO.ErrorResponse;
using Microsoft.EntityFrameworkCore;

namespace ElGato_API.Services
{
    public class CommunityService : ICommunityService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CommunityService> _logger;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        public CommunityService(AppDbContext context, ILogger<CommunityService> logger, IDbContextFactory<AppDbContext> contextFactory) 
        { 
            _context = context;
            _logger = logger;
            _contextFactory = contextFactory;
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

        public async Task<bool> CheckIfProfileIsAcessibleForUser(string userAskingId, string userCheckingId)
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
                    return false;
                }

                if (blockedByTask.Result || blockingTask.Result)
                    return false;

                if (isPrivateTask.Result == true && !isFriendTask.Result)
                    return false;

                return true;

            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Checking if profile is acessible failed. UserId: {userAskingId} CheckingUserId: {userCheckingId} Method: {nameof(CheckIfProfileIsAcessibleForUser)}");
                return false;
            }
        }
        public async Task<BasicErrorResponse> FollowUser(string userId, string userToFollowId)
        {
            try
            {
                var followed = await _context.UserFollower.AnyAsync(a=>a.FollowerId == userId && a.FolloweeId == userToFollowId);
                if (followed)
                {
                    return new BasicErrorResponse() { ErrorCode = ErrorCodes.None, Success = true, ErrorMessage = "Already followed" };
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
                    return new BasicErrorResponse()
                    {
                        ErrorCode = ErrorCodes.NotFound,
                        ErrorMessage = "Couldnt find user with given id",
                        Success = false
                    };
                } 

                user.FollowingCount += 1;
                userToFollow.FollowersCount += 1;
                _context.UserFollower.Add(userFollow);

                await _context.SaveChangesAsync();

                return new BasicErrorResponse()
                {
                    ErrorCode = ErrorCodes.None,
                    ErrorMessage = "Sucess",
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while tryinh to follow user. UserId: {userId} FollowUserId: {userToFollowId} Method: {nameof(FollowUser)}");
                return new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An error occured: {ex.Message}", Success = false };
            }
        }

        public async Task<BasicErrorResponse> UnFollowUser(string userId, string userToUnfollowId)
        {
            try
            {
                var followed = await _context.UserFollower.FirstOrDefaultAsync(a => a.FollowerId == userId && a.FolloweeId == userToUnfollowId);
                if (followed == null)
                {
                    return new BasicErrorResponse() { ErrorCode = ErrorCodes.None, Success = true, ErrorMessage = "Already not following." };
                }

                var user = await _context.AppUser.FirstOrDefaultAsync(a => a.Id == userId);
                var userToUnFollow = await _context.AppUser.FirstOrDefaultAsync(a => a.Id == userToUnfollowId);
                if (user == null || userToUnFollow == null)
                {
                    return new BasicErrorResponse()
                    {
                        ErrorCode = ErrorCodes.NotFound,
                        ErrorMessage = "Couldnt find user with given id",
                        Success = false
                    };
                }

                _context.UserFollower.Remove(followed);
                user.FollowingCount -= 1;
                userToUnFollow.FollowersCount -= 1;
                await _context.SaveChangesAsync();

                return new BasicErrorResponse()
                {
                    ErrorCode = ErrorCodes.None,
                    ErrorMessage = "Sucess",
                    Success = true
                };
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to unfollow user. UserId: {userId} UnfollowUserId: {userToUnfollowId} Method: {nameof(UnFollowUser)}");
                return new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An error occured: {ex.Message}", Success = false };
            }
        }
        public async Task<BasicErrorResponse> BlockUser(string userId, string userToBlockId)
        {
            try
            {
                var userToBlock = await _context.AppUser.FirstOrDefaultAsync( a => a.Id == userToBlockId);
                var user = await _context.AppUser.FirstOrDefaultAsync(a=>a.Id == userId);

                if(userToBlock == null || user == null)
                {
                    return new BasicErrorResponse()
                    {
                        ErrorCode = ErrorCodes.NotFound,
                        ErrorMessage = "Couldn't find any user with given id.",
                        Success = false
                    };
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
                    return new BasicErrorResponse()
                    {
                        ErrorCode = ErrorCodes.AlreadyExists,
                        ErrorMessage = "User is already blocked.",
                        Success = false
                    };
                }

                var newBlockRecord = new UserBlock()
                {
                    BlockerId = userId,
                    BlockedId = userToBlockId,
                };

                _context.UserBlock.Add(newBlockRecord);
                await _context.SaveChangesAsync();

                return new BasicErrorResponse() { ErrorCode = ErrorCodes.None, Success = true, ErrorMessage = "Sucess" };
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to block user. UserId: {userId} BlockingUserId: {userToBlockId} Method: {nameof(BlockUser)}");
                return new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An error occured: {ex.Message}", Success = false };
            }
        }
        public async Task<BasicErrorResponse> UnBlockUser(string userId, string userToUnblockId)
        {
            try
            {
                var userToUnblock = await _context.AppUser.FirstOrDefaultAsync(a => a.Id == userToUnblockId);
                var user = await _context.AppUser.FirstOrDefaultAsync(a => a.Id == userId);

                if (userToUnblock == null || user == null)
                {
                    return new BasicErrorResponse()
                    {
                        ErrorCode = ErrorCodes.NotFound,
                        ErrorMessage = "Couldn't find any user with given id.",
                        Success = false
                    };
                }

                var record = await _context.UserBlock.FirstOrDefaultAsync(b => b.BlockerId == userId && b.BlockedId == userToUnblockId);
                if(record != null)
                {
                    _context.UserBlock.Remove(record);
                    await _context.SaveChangesAsync();
                }

                return new BasicErrorResponse() { ErrorCode = ErrorCodes.None, Success = true, ErrorMessage = "Sucess" };
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to unlock user. UserId: {userId} BlockingUserId: {userToUnblockId} Method: {nameof(UnBlockUser)}");
                return new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An error occured: {ex.Message}", Success = false };
            }
        }

        public async Task<(UserFollowersVMO data, BasicErrorResponse error)> GetUserFollowerLists(string userId, bool onlyFollowed)
        {
            try
            {
                var vmo = new UserFollowersVMO();

                var user = await _context.AppUser.Include(u => u.Followers).ThenInclude(f => f.Follower)
                                                    .Include(u => u.Following).ThenInclude(f => f.Followee)
                                                        .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    return (new UserFollowersVMO(), new BasicErrorResponse
                    {
                        ErrorCode = ErrorCodes.NotFound,
                        ErrorMessage = "User not found",
                        Success = false
                    });
                }

                if (!onlyFollowed)
                {
                    vmo.Followers = user.Followers.Select(f => new UserFollowersList
                    {
                        UserId = f.Follower.Id,
                        Name = f.Follower.Name??"User",
                        Pfp = f.Follower.Pfp,
                        IsFollowed = user.Following.Any(ff => ff.FolloweeId == f.FollowerId)
                    }).ToList();
                }

                vmo.Followed = user.Following.Select(f => new UserFollowersList
                {
                    UserId = f.Followee.Id,
                    Name = f.Followee.Name??"User",
                    Pfp = f.Followee.Pfp,
                    IsFollowed = true
                }).ToList();

                return (vmo, new BasicErrorResponse { Success = true, ErrorMessage = "Sucess", ErrorCode = ErrorCodes.NotFound });
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get user follow lists. UserId: {userId} Method: {nameof(GetUserFollowerLists)}");
                return (new UserFollowersVMO(), new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An error occured: {ex.Message}", Success = false });
            }
        }

        public async Task<(BlockListVMO data, BasicErrorResponse error)> GetUserBlockList(string userId)
        {
            try
            {
                var vmo = new BlockListVMO();
                var user = await _context.AppUser.Include(u => u.BlockedUsers).ThenInclude(a=>a.Blocked).FirstOrDefaultAsync(a=>a.Id == userId);
                if (user == null)
                {
                    _logger.LogWarning($"Trying to acess non existing user Method: {nameof(GetUserBlockList)}");
                    return (vmo, new BasicErrorResponse() { ErrorCode = ErrorCodes.NotFound, ErrorMessage = $"User with id: {userId} Not found.", Success = false });
                }

                vmo.BlockList = user.BlockedUsers.Select(a => new BlockList
                {
                    Name = a.Blocked.Name??"User",
                    Pfp = a.Blocked.Pfp,       
                    UserId = a.Blocked.Id,
                }).ToList();

                return (vmo, new BasicErrorResponse() { Success = true, ErrorCode = ErrorCodes.None, ErrorMessage = "Sucess" });
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get user block list. UserId: {userId} Method: {nameof(GetUserBlockList)}");
                return (new BlockListVMO(), new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An error occured: {ex.Message}", Success = false });
            }
        }
    }
}
