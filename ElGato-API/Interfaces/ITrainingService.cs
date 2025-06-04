using ElGato_API.Models.Training;
using ElGato_API.ModelsMongo.History;
using ElGato_API.VM.Training;
using ElGato_API.VMO.ErrorResponse;
using ElGato_API.VMO.Training;
using MongoDB.Driver;

namespace ElGato_API.Interfaces
{
    public interface ITrainingService
    {
        Task<(BasicErrorResponse error, List<ExerciseVMO>? data)> GetAllExercises();
        Task<(BasicErrorResponse error, List<LikedExercisesVMO>? data)> GetAllLikedExercises(string userId);
        Task<(BasicErrorResponse error, TrainingDayVMO? data)> GetUserTrainingDay(string userId, DateTime date);
        Task<(BasicErrorResponse error, SavedTrainingsVMO? data)> GetSavedTrainings(string userId);
        Task<double> GetTotalExerciseWeightValue(string userId, DateTime date, List<int> exercisePublicIds, IClientSessionHandle session = null);
        Task<List<int>> GetExerciseInTrainingDayPublicIds(string userId, DateTime date, IClientSessionHandle session = null);
        Task<BasicErrorResponse> SaveTraining(string userId, SaveTrainingVM model);
        Task<BasicErrorResponse> AddExercisesToTrainingDay(string userId, AddExerciseToTrainingVM model);
        Task<BasicErrorResponse> LikeExercise(string userId, LikeExerciseVM model, IClientSessionHandle session = null);
        Task<BasicErrorResponse> RemoveExercisesFromLiked(string userId, List<LikeExerciseVM> model);
        Task<BasicErrorResponse> WriteSeriesForAnExercise(string userId, AddSeriesToAnExerciseVM model, IClientSessionHandle session = null);
        Task<BasicErrorResponse> AddSavedTrainingToTrainingDay(string userId, AddSavedTrainingToTrainingDayVM model);
        Task<BasicErrorResponse> UpdateExerciseHistory(string userId, HistoryUpdateVM model, DateTime date, IClientSessionHandle session = null);
        Task<BasicErrorResponse> RemoveSeriesFromAnExercise(string userId, RemoveSeriesFromExerciseVM model, IClientSessionHandle session = null);
        Task<BasicErrorResponse> RemoveExerciseFromTrainingDay(string userId, RemoveExerciseFromTrainingDayVM model, IClientSessionHandle session = null);
        Task<BasicErrorResponse> UpdateExerciseLikedStatus(string userId, string exerciseName, MuscleType type);
        Task<BasicErrorResponse> UpdateExerciseSeries(string userId, UpdateExerciseSeriesVM model, IClientSessionHandle session = null);
        Task<BasicErrorResponse> UpdateSavedTrainingName(string userId, UpdateSavedTrainingName model);
        Task<BasicErrorResponse> RemoveTrainingsFromSaved(string userId, RemoveSavedTrainingsVM model);
        Task<BasicErrorResponse> RemoveExercisesFromSavedTraining(string userId, DeleteExercisesFromSavedTrainingVM model);
        Task<BasicErrorResponse> AddPersonalExerciseRecordToHistory(string userId, string exerciseName, MuscleType type, IClientSessionHandle session = null);       
    }
}
