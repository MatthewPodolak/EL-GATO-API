using ElGato_API.Controllers;
using ElGato_API.Models.User;
using ElGato_API.VM.UserData;
using ElGato_API.VMO.ErrorResponse;
using ElGato_API.VMO.User;
using MongoDB.Driver;

namespace ElGato_API.Interfaces
{
    public interface IUserService
    {
        Task<(ErrorResponse error, UserCalorieIntake model)> GetUserCalories(string userId);
        Task<(ErrorResponse error, UserCalorieIntake? model)> GetCurrentCalories(string userId, DateTime date);
        Task<(ErrorResponse error, double weight)> GetCurrentUserWeight(string userId);
        Task<(ErrorResponse error, double water)> GetCurrentWaterIntake(string userId, DateTime date);
        Task<(ErrorResponse error, string? data)> GetSystem(string userId);
        Task<(ErrorResponse error, UserLayoutVMO? data)> GetUserLayout(string userId);
        Task<(ErrorResponse error, ExercisePastDataVMO? data)> GetPastExerciseData(string userId, string exerciseName, string period = "all");
        Task<(ErrorResponse error, MuscleUsageDataVMO? data)> GetMuscleUsageData(string userId, string period = "all");
        Task<(ErrorResponse error, MakroDataVMO? data)> GetPastMakroData(string userId, string period = "all");
        Task<(ErrorResponse error, DailyMakroDistributionVMO? data)> GetDailyMakroDisturbtion(string userId, DateTime date);
        Task<ErrorResponse> UpdateLayout(string userId, UserLayoutVM model);
        Task<ErrorResponse> AddToUserStatistics(string userId, List<UserStatisticsVM> model, IClientSessionHandle session = null, bool caloriesNormal = false);
        Task<(ErrorResponse error, string? newPfpUrl)> UpdateProfileInformation(string userId, UserProfileInformationVM model);
        Task<ErrorResponse> ChangeProfileVisilibity(string userId);
    }
}
