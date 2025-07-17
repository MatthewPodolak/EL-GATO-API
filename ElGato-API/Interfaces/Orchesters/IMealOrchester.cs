using ElGato_API.VM.Meal;
using ElGato_API.VMO.Achievments;

namespace ElGato_API.Interfaces.Orchesters
{
    public interface IMealOrchester
    {
        Task<AchievmentResponse> ProcessAndPublishMeal(string userId, PublishMealVM model);
    }
}
