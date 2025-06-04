using ElGato_API.Data;
using ElGato_API.VM.Achievments;
using ElGato_API.VM.UserData;
using ElGato_API.VMO.Achievments;
using ElGato_API.VMO.Cardio;
using ElGato_API.VMO.ErrorResponse;

namespace ElGato_API.Interfaces
{
    public interface IAchievmentService
    {
        Task<(BasicErrorResponse error, string? achievmentName)> GetCurrentAchivmentIdFromFamily(string achievmentFamily, string userId, AppDbContext? context = null);
        Task<(BasicErrorResponse error, AchievmentResponse? ach)> IncrementAchievmentProgress(string achievmentName, string userId, int incValue, AppDbContext? context = null);

        //badges
        Task<(BasicErrorResponse error, List<ChallengeVMO>? data)> GetActiveChallenges(string userId);
        Task<(BasicErrorResponse error, List<ActiveChallengeVMO>? data)> GetUserActiveChallenges(string userId);
        Task<BasicErrorResponse> CheckAndAddBadgeProgressForUser(string userId, BadgeIncDataVM model, AppDbContext? context = null);

        //HK
        Task<(BasicErrorResponse error, AchievmentResponse? ach)> AddFromHealthConnectToStatisticsAndIncrementAchievments(string userId, List<UserStatisticsVM> data);
    }
}
