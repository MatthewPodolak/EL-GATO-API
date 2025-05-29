using ElGato_API.VMO.Community;
using ElGato_API.VMO.ErrorResponse;

namespace ElGato_API.Interfaces
{
    public interface ICommunityService
    {
        Task<bool> UserExists(string userId);
        Task<bool> CheckIfUserIsBlockedBy(string userId, string checkingUserId);
        Task<bool> CheckIfUserIsBlocking(string userId, string checkingUserId);       
        Task<bool> CheckIfProfileIsAcessibleForUser(string userAskingId, string userCheckingId);
        Task<BasicErrorResponse> FollowUser(string userId, string userToFollowId);
        Task<BasicErrorResponse> UnFollowUser(string userId, string userToUnfollowId);
        Task<BasicErrorResponse> BlockUser(string userId, string userToBlockId);
        Task<BasicErrorResponse> UnBlockUser(string userId, string userToUnblockId);
        Task<(UserFollowersVMO data, BasicErrorResponse error)> GetUserFollowerLists(string userId, bool onlyFollowed);
        Task<(BlockListVMO data, BasicErrorResponse error)> GetUserBlockList(string userId);
        Task<(UserSearchVMO data, BasicErrorResponse error)> SearchForUsers(string userId, string query, int limit = 10);
    }
}
