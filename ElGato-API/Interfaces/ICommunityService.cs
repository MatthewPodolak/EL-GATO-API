using ElGato_API.VM.Community;
using ElGato_API.VMO.Community;
using ElGato_API.VMO.ErrorResponse;

namespace ElGato_API.Interfaces
{
    public interface ICommunityService
    {
        Task<bool> UserExists(string userId);
        Task<bool> CheckIfUserIsBlockedBy(string userId, string checkingUserId);
        Task<bool> CheckIfUserIsBlocking(string userId, string checkingUserId);
        Task<bool> CheckIfUserFollowUser(string userId, string checkingUserId);
        Task<AcessibleVMO> CheckIfProfileIsAcessibleForUser(string userAskingId, string userCheckingId);
        Task<BasicErrorResponse> RequestFollow(string userAskingId, string userTargetId);
        Task<BasicErrorResponse> FollowUser(string userId, string userToFollowId);
        Task<BasicErrorResponse> UnFollowUser(string userId, string userToUnfollowId);
        Task<BasicErrorResponse> BlockUser(string userId, string userToBlockId);
        Task<BasicErrorResponse> UnBlockUser(string userId, string userToUnblockId);
        Task<BasicErrorResponse> RemoveFollowRequest(string userId, string userIdToRemoveRequestFrom);
        Task<BasicErrorResponse> RespondToFollowRequest(string userId, RespondToFollowVM model);
        Task<(FriendsLeaderboardVMO data, BasicErrorResponse error)> GetFriendsLeaderboards(string userId);
        Task<(UserFollowersVMO data, BasicErrorResponse error)> GetUserFollowerLists(string userId, bool onlyFollowed, string askingUserId = null);
        Task<(BlockListVMO data, BasicErrorResponse error)> GetUserBlockList(string userId);
        Task<(UserSearchVMO data, BasicErrorResponse error)> SearchForUsers(string userId, string query, int limit = 10);
        Task<(UserProfileDataVMO data, BasicErrorResponse error)> GetUserProfileData(string userId, string askingUserId, bool full = true);
        Task<(FollowersRequestVMO data, BasicErrorResponse erro)> GetFollowersRequests(string userId);
        Task<(BasicErrorResponse error, string profilePcitrue)> GetUserProfilePicture(string userId);
    }
}
