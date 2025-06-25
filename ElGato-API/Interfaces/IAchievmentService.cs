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
        Task<(ErrorResponse error, string? achievmentName)> GetCurrentAchivmentIdFromFamily(string achievmentFamily, string userId, AppDbContext? context = null);
        Task<(ErrorResponse error, AchievmentResponse? ach)> IncrementAchievmentProgress(string achievmentName, string userId, int incValue, AppDbContext? context = null);

        //badges
        Task<(ErrorResponse error, List<ChallengeVMO>? data)> GetActiveChallenges(string userId);
        Task<(ErrorResponse error, List<ActiveChallengeVMO>? data)> GetUserActiveChallenges(string userId);
        Task<ErrorResponse> CheckAndAddBadgeProgressForUser(string userId, BadgeIncDataVM model, AppDbContext? context = null);

        //HK
        Task<(ErrorResponse error, AchievmentResponse? ach)> AddFromHealthConnectToStatisticsAndIncrementAchievments(string userId, List<UserStatisticsVM> data);
    }
}
