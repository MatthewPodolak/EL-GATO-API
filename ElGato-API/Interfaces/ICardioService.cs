using ElGato_API.VM.Cardio;
using ElGato_API.VM.UserData;
using ElGato_API.VMO.Cardio;
using ElGato_API.VMO.ErrorResponse;
using MongoDB.Driver;

namespace ElGato_API.Interfaces
{
    public interface ICardioService
    {
        Task<(ErrorResponse error, CardioTrainingDayVMO data)> GetTrainingDay(string userId, DateTime date);
        Task<ErrorResponse> AddExerciseToTrainingDay(string userId, AddCardioExerciseVM model, IClientSessionHandle? session = null);
        Task<ErrorResponse> JoinChallenge(string userId, int challengeId);
        Task<ErrorResponse> ChangeExerciseVisilibity(string userId, ChangeExerciseVisilibityVM model);
        Task<ErrorResponse> DeleteExercisesFromCardioTrainingDay(string userId, DeleteExercisesFromCardioTrainingVM model, IClientSessionHandle? session = null);
        Task<(ErrorResponse error, List<UserStatisticsVM> data)> GetStatisticsDataFromExercise(string userId, List<int> exerciseIds, DateTime date, IClientSessionHandle? session = null);
    }
}
