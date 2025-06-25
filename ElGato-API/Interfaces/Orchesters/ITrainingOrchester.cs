using ElGato_API.VM.Training;
using ElGato_API.VMO.ErrorResponse;

namespace ElGato_API.Interfaces.Orchesters
{
    public interface ITrainingOrchester
    {
        Task<ErrorResponse> AddSeriesToAnExercise(string userId, List<AddSeriesToAnExerciseVM> model);
        Task<ErrorResponse> UpdateExerciseSeries(string userId, List<UpdateExerciseSeriesVM> model);
        Task<ErrorResponse> RemoveSeriesFromAnExercise(string userId, List<RemoveSeriesFromExerciseVM> model);
        Task<ErrorResponse> RemoveExercisesFromTrainingDay(string userId, List<RemoveExerciseFromTrainingDayVM> model);
    }
}
