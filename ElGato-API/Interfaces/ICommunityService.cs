using ElGato_API.VMO.ErrorResponse;

namespace ElGato_API.Interfaces
{
    public interface ICommunityService
    {
        Task<bool> CheckIfUserIsBlockedBy(string userId, string checkingUserId);
        Task<bool> CheckIfUserIsBlocking(string userId, string checkingUserId);
        Task<BasicErrorResponse> FollowUser(string userId, string userToFollowId);
        Task<BasicErrorResponse> UnFollowUser(string userId, string userToUnfollowId);
        Task<BasicErrorResponse> BlockUser(string userId, string userToBlockId);
        Task<BasicErrorResponse> UnBlockUser(string userId, string userToUnblockId);
    }
}
