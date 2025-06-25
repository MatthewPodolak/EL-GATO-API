using ElGato_API.Models.User;
using ElGato_API.ModelsMongo.Meal;
using ElGato_API.VM.Meal;
using ElGato_API.VMO.Achievments;
using ElGato_API.VMO.ErrorResponse;
using ElGato_API.VMO.Meals;
using MongoDB.Bson;

namespace ElGato_API.Interfaces
{
    public interface IMealService
    {
        Task<(List<SimpleMealVMO> res, ErrorResponse error)> GetByMainCategory(string userId, List<string> LikedMeals, List<string> SavedMeals, string? category, int? qty = 5, int? pageNumber = 1);
        Task<(List<SimpleMealVMO> res, ErrorResponse error)> GetByLowMakro(string userId, List<string> LikedMeals, List<string> SavedMeals, string makroComponent, int? qty = 5, int? pageNumber = 1);
        Task<(List<SimpleMealVMO> res, ErrorResponse error)> GetByHighMakro(string userId, List<string> LikedMeals, List<string> SavedMeals, string makroComponent, int? qty = 5, int? pageNumber = 1);
        Task<(List<SimpleMealVMO> res, ErrorResponse error)> GetMostLiked(string userId, List<string> LikedMeals, List<string> SavedMeals, int? qty = 5, int? pageNumber = 1);
        Task<(List<SimpleMealVMO> res, ErrorResponse error)> GetRandom(string userId, List<string> LikedMeals, List<string> SavedMeals, int? qty = 5, int? pageNumber = 1);

        Task<(MealLikesDocument res, ErrorResponse error)> GetUserMealLikeDoc(string userId);
        Task<ErrorResponse> LikeMeal(string userId, string mealId);
        Task<ErrorResponse> SaveMeal(string userId, string mealId);

        Task<(ErrorResponse error, List<SimpleMealVMO> res)> GetUserLikedMeals(string userId);
        Task<(ErrorResponse error, List<SimpleMealVMO> res)> GetUserSavedMeals(string userId);
        Task<(ErrorResponse error, List<SimpleMealVMO> res)> Search(string userId, SearchMealVM model);
        Task<(ErrorResponse error, AchievmentResponse? ach)> ProcessAndPublishMeal(string userId, PublishMealVM model);
        Task<(ErrorResponse error, List<SimpleMealVMO>? res)> GetOwnMeals(string userId, List<string> LikedMeals, List<string> SavedMeals);
        Task<ErrorResponse> DeleteMeal(string userId, ObjectId mealId);
        Task<(List<SimpleMealVMO> data, ErrorResponse error)> GetUserRecipes(string userId, int count, int skip, List<string> LikedMeals, List<string> SavedMeals);
    }
}
