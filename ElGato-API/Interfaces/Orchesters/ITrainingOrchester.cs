using ElGato_API.VM.Training;
using ElGato_API.VMO.ErrorResponse;

namespace ElGato_API.Interfaces.Orchesters
{
    public interface ITrainingOrchester
    {
        Task<BasicErrorResponse> AddSeriesToAnExercise(string userId, List<AddSeriesToAnExerciseVM> model);
        Task<BasicErrorResponse> UpdateExerciseSeries(string userId, List<UpdateExerciseSeriesVM> model);
        Task<BasicErrorResponse> RemoveSeriesFromAnExercise(string userId, List<RemoveSeriesFromExerciseVM> model);
        Task<BasicErrorResponse> RemoveExercisesFromTrainingDay(string userId, List<RemoveExerciseFromTrainingDayVM> model);
    }
}
