﻿using ElGato_API.Models.User;
using ElGato_API.VMO.ErrorResponse;
using ElGato_API.VMO.User;

namespace ElGato_API.Interfaces
{
    public interface IUserService
    {
        Task<(BasicErrorResponse error, UserCalorieIntake model)> GetUserCalories(string userId);
        Task<(BasicErrorResponse error, UserCalorieIntake? model)> GetCurrentCalories(string userId, DateTime date);
        Task<(BasicErrorResponse error, double water)> GetCurrentWaterIntake(string userId, DateTime date);
        Task<(BasicErrorResponse error, string? data)> GetSystem(string userId);
        Task<(BasicErrorResponse error, UserLayoutVMO? data)> GetUserLayout(string userId);
        Task<(BasicErrorResponse error, ExercisePastDataVMO? data)> GetPastExerciseData(string userId, string exerciseName, string period = "all");
        Task<(BasicErrorResponse error, MuscleUsageDataVMO? data)> GetMuscleUsageData(string userId, string period = "all");
        Task<(BasicErrorResponse error, MakroDataVMO? data)> GetPastMakroData(string userId, string period = "all");
        Task<(BasicErrorResponse error, DailyMakroDistributionVMO? data)> GetDailyMakroDisturbtion(string userId, DateTime date);
    }
}
