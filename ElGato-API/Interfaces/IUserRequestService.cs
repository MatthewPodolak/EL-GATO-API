using ElGato_API.VM.Requests;
using ElGato_API.VMO.ErrorResponse;

namespace ElGato_API.Interfaces
{
    public interface IUserRequestService
    {
        Task<ErrorResponse> RequestAddIngredient(string userId, AddProductRequestVM model);
        Task<ErrorResponse> RequestReportIngredient(string userId, IngredientReportRequestVM model);
        Task<ErrorResponse> RequestReportMeal(string userId, ReportMealRequestVM model);
        Task<ErrorResponse> RequestReportUser(string userId, ReportUserVM model);
    }
}
