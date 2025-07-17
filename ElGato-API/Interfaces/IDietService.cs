using ElGato_API.ModelsMongo.Diet;
using ElGato_API.VM.Diet;
using ElGato_API.VMO.Diet;
using ElGato_API.VMO.ErrorResponse;
using ElGato_API.VMO.Questionary;

namespace ElGato_API.Interfaces
{
    public interface IDietService
    {
        Task<ErrorResponse> AddNewMeal(string userId, string mealName, DateTime date);
        Task<ErrorResponse> AddIngredientsToMeals(string userId, AddIngridientsVM model);
        Task<ErrorResponse> AddWater(string userId, int water, DateTime date);
        Task<ErrorResponse> AddMealToSavedMeals(string userId, SaveIngridientMealVM model);
        Task<ErrorResponse> AddMealFromSavedMeals(string userId, AddMealFromSavedVM model);

        Task<(IngridientVMO? ingridient, ErrorResponse error)> GetIngridientByEan(string ean);
        Task<(List<IngridientVMO>? ingridients, ErrorResponse error)> GetListOfIngridientsByName(string name, int count = 20, string? afterCode = null);
        Task<(ErrorResponse errorResponse, DietDocVMO model)> GetUserDoc(string userId);
        Task<(ErrorResponse errorResponse, DietDayVMO model)> GetUserDietDay(string userId, DateTime date);
        Task<(ErrorResponse errorResponse, List<MealPlan>? model)> GetSavedMeals(string userId);

        Task<ErrorResponse> DeleteMeal(string userId, int publicId, DateTime date);
        Task<ErrorResponse> DeleteIngridientFromMeal(string userId, RemoveIngridientVM model);
        Task<ErrorResponse> RemoveMealFromSaved(string userId, string name);
        Task<ErrorResponse> DeleteMealsFromSaved(string userId, DeleteSavedMealsVM model);

        Task<ErrorResponse> UpdateMealName(string userId, UpdateMealNameVM model);
        Task<ErrorResponse> UpdateIngridientWeightValue(string userId, UpdateIngridientVM model);
        Task<ErrorResponse> UpdateSavedMealIngridientWeight(string userId, UpdateSavedMealWeightVM model);

        CalorieIntakeVMO CalculateCalories(QuestionaryVM questionary);
    }
}
