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
        Task<(List<SimpleMealVMO> res, BasicErrorResponse error)> GetByMainCategory(string userId, List<string> LikedMeals, List<string> SavedMeals, string? category, int? qty = 5, int? pageNumber = 1);
        Task<(List<SimpleMealVMO> res, BasicErrorResponse error)> GetByLowMakro(string userId, List<string> LikedMeals, List<string> SavedMeals, string makroComponent, int? qty = 5, int? pageNumber = 1);
        Task<(List<SimpleMealVMO> res, BasicErrorResponse error)> GetByHighMakro(string userId, List<string> LikedMeals, List<string> SavedMeals, string makroComponent, int? qty = 5, int? pageNumber = 1);
        Task<(List<SimpleMealVMO> res, BasicErrorResponse error)> GetMostLiked(string userId, List<string> LikedMeals, List<string> SavedMeals, int? qty = 5, int? pageNumber = 1);
        Task<(List<SimpleMealVMO> res, BasicErrorResponse error)> GetRandom(string userId, List<string> LikedMeals, List<string> SavedMeals, int? qty = 5, int? pageNumber = 1);

        Task<(MealLikesDocument res, BasicErrorResponse error)> GetUserMealLikeDoc(string userId);
        Task<BasicErrorResponse> LikeMeal(string userId, string mealId);
        Task<BasicErrorResponse> SaveMeal(string userId, string mealId);

        Task<(BasicErrorResponse error, List<SimpleMealVMO> res)> GetUserLikedMeals(string userId);
        Task<(BasicErrorResponse error, List<SimpleMealVMO> res)> GetUserSavedMeals(string userId);
        Task<(BasicErrorResponse error, List<SimpleMealVMO> res)> Search(string userId, SearchMealVM model);
        Task<(BasicErrorResponse error, AchievmentResponse? ach)> ProcessAndPublishMeal(string userId, PublishMealVM model);
        Task<(BasicErrorResponse error, List<SimpleMealVMO>? res)> GetOwnMeals(string userId, List<string> LikedMeals, List<string> SavedMeals);
        Task<BasicErrorResponse> DeleteMeal(string userId, ObjectId mealId);
        Task<(List<SimpleMealVMO> data, BasicErrorResponse error)> GetUserRecipes(string userId, int count, int skip, List<string> LikedMeals, List<string> SavedMeals);
    }
}
