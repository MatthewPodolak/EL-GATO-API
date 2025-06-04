using ElGato_API.VM.Cardio;
using ElGato_API.VM.Training;
using ElGato_API.VMO.Achievments;
using ElGato_API.VMO.ErrorResponse;

namespace ElGato_API.Interfaces.Orchesters
{
    public interface ICardioOrchester
    {
        Task<AchievmentResponse> AddExerciseToTrainingDay(string userId, AddCardioExerciseVM model);
        Task<BasicErrorResponse> DeleteExercisesFromCardioTrainingDay(string userId, DeleteExercisesFromCardioTrainingVM model);
    }
}
