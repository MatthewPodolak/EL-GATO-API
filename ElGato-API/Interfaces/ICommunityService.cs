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
        Task<ErrorResponse> RequestFollow(string userAskingId, string userTargetId);
        Task<ErrorResponse> FollowUser(string userId, string userToFollowId);
        Task<ErrorResponse> UnFollowUser(string userId, string userToUnfollowId);
        Task<ErrorResponse> BlockUser(string userId, string userToBlockId);
        Task<ErrorResponse> UnBlockUser(string userId, string userToUnblockId);
        Task<ErrorResponse> RemoveFollowRequest(string userId, string userIdToRemoveRequestFrom);
        Task<ErrorResponse> RespondToFollowRequest(string userId, RespondToFollowVM model);
        Task<(FriendsLeaderboardVMO data, ErrorResponse error)> GetFriendsLeaderboards(string userId);
        Task<(UserFollowersVMO data, ErrorResponse error)> GetUserFollowerLists(string userId, bool onlyFollowed, string askingUserId = null);
        Task<(BlockListVMO data, ErrorResponse error)> GetUserBlockList(string userId);
        Task<(UserSearchVMO data, ErrorResponse error)> SearchForUsers(string userId, string query, int limit = 10);
        Task<(UserProfileDataVMO data, ErrorResponse error)> GetUserProfileData(string userId, string askingUserId, bool full = true);
        Task<(FollowersRequestVMO data, ErrorResponse erro)> GetFollowersRequests(string userId);
        Task<(ErrorResponse error, string profilePcitrue)> GetUserProfilePicture(string userId);
    }
}
