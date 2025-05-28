using ElGato_API.VM.Requests;
using ElGato_API.VMO.ErrorResponse;

namespace ElGato_API.Interfaces
{
    public interface IUserRequestService
    {
        Task<BasicErrorResponse> RequestAddIngredient(string userId, AddProductRequestVM model);
        Task<BasicErrorResponse> RequestReportIngredient(string userId, IngredientReportRequestVM model);
        Task<BasicErrorResponse> RequestReportMeal(string userId, ReportMealRequestVM model);
        Task<BasicErrorResponse> RequestReportUser(string userId, ReportUserVM model);
    }
}
