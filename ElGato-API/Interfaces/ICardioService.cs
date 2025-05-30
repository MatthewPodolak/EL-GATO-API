﻿using ElGato_API.VM.Cardio;
using ElGato_API.VMO.Cardio;
using ElGato_API.VMO.ErrorResponse;

namespace ElGato_API.Interfaces
{
    public interface ICardioService
    {
        Task<(BasicErrorResponse error, CardioTrainingDayVMO data)> GetTrainingDay(string userId, DateTime date);
        Task<BasicErrorResponse> AddExerciseToTrainingDay(string userId, AddCardioExerciseVM model);
        Task<BasicErrorResponse> JoinChallenge(string userId, int challengeId);
        Task<BasicErrorResponse> ChangeExerciseVisilibity(string userId, ChangeExerciseVisilibityVM model);
        Task<BasicErrorResponse> DeleteExercisesFromCardioTrainingDay(string userId, DeleteExercisesFromCardioTrainingVM model);
    }
}
