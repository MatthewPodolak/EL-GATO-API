using ElGato_API.Data;
using ElGato_API.Interfaces;
using ElGato_API.Models.User;
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

    }
}
