using ElGato_API.VM.Cardio;
using ElGato_API.VM.UserData;
using ElGato_API.VMO.Cardio;
using ElGato_API.VMO.ErrorResponse;
using MongoDB.Driver;

namespace ElGato_API.Interfaces
{
    public interface ICardioService
    {
        Task<(BasicErrorResponse error, CardioTrainingDayVMO data)> GetTrainingDay(string userId, DateTime date);
        Task<BasicErrorResponse> AddExerciseToTrainingDay(string userId, AddCardioExerciseVM model, IClientSessionHandle? session = null);
        Task<BasicErrorResponse> JoinChallenge(string userId, int challengeId);
        Task<BasicErrorResponse> ChangeExerciseVisilibity(string userId, ChangeExerciseVisilibityVM model);
        Task<BasicErrorResponse> DeleteExercisesFromCardioTrainingDay(string userId, DeleteExercisesFromCardioTrainingVM model, IClientSessionHandle? session = null);
        Task<(BasicErrorResponse error, List<UserStatisticsVM> data)> GetStatisticsDataFromExercise(string userId, List<int> exerciseIds, DateTime date, IClientSessionHandle? session = null);
    }
}
