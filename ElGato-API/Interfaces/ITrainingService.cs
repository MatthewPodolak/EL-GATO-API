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
        Task<(ErrorResponse error, List<ExerciseVMO>? data)> GetAllExercises();
        Task<(ErrorResponse error, List<LikedExercisesVMO>? data)> GetAllLikedExercises(string userId);
        Task<(ErrorResponse error, TrainingDayVMO? data)> GetUserTrainingDay(string userId, DateTime date);
        Task<(ErrorResponse error, SavedTrainingsVMO? data)> GetSavedTrainings(string userId);
        Task<double> GetTotalExerciseWeightValue(string userId, DateTime date, List<int> exercisePublicIds, IClientSessionHandle session = null);
        Task<List<int>> GetExerciseInTrainingDayPublicIds(string userId, DateTime date, IClientSessionHandle session = null);
        Task<ErrorResponse> SaveTraining(string userId, SaveTrainingVM model);
        Task<ErrorResponse> AddExercisesToTrainingDay(string userId, AddExerciseToTrainingVM model);
        Task<ErrorResponse> LikeExercise(string userId, LikeExerciseVM model, IClientSessionHandle session = null);
        Task<ErrorResponse> RemoveExercisesFromLiked(string userId, List<LikeExerciseVM> model);
        Task<ErrorResponse> WriteSeriesForAnExercise(string userId, AddSeriesToAnExerciseVM model, IClientSessionHandle session = null);
        Task<ErrorResponse> AddSavedTrainingToTrainingDay(string userId, AddSavedTrainingToTrainingDayVM model);
        Task<ErrorResponse> UpdateExerciseHistory(string userId, HistoryUpdateVM model, DateTime date, IClientSessionHandle session = null);
        Task<ErrorResponse> RemoveSeriesFromAnExercise(string userId, RemoveSeriesFromExerciseVM model, IClientSessionHandle session = null);
        Task<ErrorResponse> RemoveExerciseFromTrainingDay(string userId, RemoveExerciseFromTrainingDayVM model, IClientSessionHandle session = null);
        Task<ErrorResponse> UpdateExerciseLikedStatus(string userId, string exerciseName, MuscleType type);
        Task<ErrorResponse> UpdateExerciseSeries(string userId, UpdateExerciseSeriesVM model, IClientSessionHandle session = null);
        Task<ErrorResponse> UpdateSavedTrainingName(string userId, UpdateSavedTrainingName model);
        Task<ErrorResponse> RemoveTrainingsFromSaved(string userId, RemoveSavedTrainingsVM model);
        Task<ErrorResponse> RemoveExercisesFromSavedTraining(string userId, DeleteExercisesFromSavedTrainingVM model);
        Task<ErrorResponse> AddPersonalExerciseRecordToHistory(string userId, string exerciseName, MuscleType type, IClientSessionHandle session = null);       
    }
}
