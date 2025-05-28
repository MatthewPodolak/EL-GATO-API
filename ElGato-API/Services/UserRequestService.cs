using ElGato_API.Data;
using ElGato_API.Interfaces;
using ElGato_API.Models.Requests;
using ElGato_API.VM.Requests;
using ElGato_API.VMO.ErrorResponse;

namespace ElGato_API.Services
{
    public class UserRequestService : IUserRequestService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UserRequestService> _logger;
        public UserRequestService(AppDbContext context, ILogger<UserRequestService> logger) 
        { 
            _context = context;
            _logger = logger;
        }
        public async Task<BasicErrorResponse> RequestAddIngredient(string userId, AddProductRequestVM model)
        {
            try
            {
                AddProductRequest request = new AddProductRequest()
                {
                    ProductBrand = model.ProductBrand,
                    ProductEan13 = model.ProductEan13,
                    ProductName = model.ProductName,
                    EnergyKcal = model.EnergyKcal,
                    Carbs = model.Carbs,
                    Fats = model.Fats,
                    Proteins = model.Proteins,
                    UserId = userId
                };

                _context.AddProductRequest.Add(request);
                await _context.SaveChangesAsync();

                return new BasicErrorResponse() { Success = true, ErrorCode = ErrorCodes.None, ErrorMessage = "Sucesfully requested ingridient addition." };

            }catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to request ingridient addition. UserId: {userId} Data: {model} Method: {nameof(RequestAddIngredient)}");
                return new BasicErrorResponse() { ErrorMessage = $"Request not succesfull. {ex.Message}", Success = false, ErrorCode = ErrorCodes.Internal };
            }
        }

        public async Task<BasicErrorResponse> RequestReportIngredient(string userId, IngredientReportRequestVM model)
        {
            try
            {
                ReportedIngredients request = new ReportedIngredients()
                {
                    Cause = model.Cause,
                    IngredientName = model.IngredientName,
                    UserId = userId,
                    IngredientId = model.IngredientId
                };

                _context.ReportedIngredients.Add(request);
                await _context.SaveChangesAsync();

                return new BasicErrorResponse() { Success = true, ErrorCode = ErrorCodes.None, ErrorMessage = "Sucesfully created report request" };
            }
            catch (Exception ex) {
                _logger.LogError(ex, $"Failed while trying to report ingredient. UserId: {userId} Data: {model} Method: {nameof(RequestReportIngredient)}");
                return new BasicErrorResponse() { ErrorMessage = $"Request not succesfull {ex.Message}", Success = false, ErrorCode = ErrorCodes.Internal };
            }
        
        }

        public async Task<BasicErrorResponse> RequestReportMeal(string userId, ReportMealRequestVM model)
        {
            try
            {
                ReportedMeals request = new ReportedMeals()
                {
                    Cause= model.Cause,
                    MealId = model.MealId,
                    MealName = model.MealName,
                    UserId=userId,
                };

                _context.ReportedMeals.Add(request);
                await _context.SaveChangesAsync();

                return new BasicErrorResponse() { Success = true, ErrorMessage = "Meal report request sucesfull.", ErrorCode = ErrorCodes.None };
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, $"Failed while trying to report meal. UserId: {userId} Data: {model} Method: {nameof(RequestReportMeal)}");
                return new BasicErrorResponse() { ErrorMessage = $"Request not succedull, internal error {ex.Message}", Success = false, ErrorCode = ErrorCodes.Internal };
            }
        }

        public async Task<BasicErrorResponse> RequestReportUser(string userId, ReportUserVM model)
        {
            try
            {
                var newReport = new ReportedUsers()
                {
                    Case = model.ReportCase,
                    CaseDescription = model.OtherDescription??string.Empty,
                    ReportedUserId = model.ReportedUserId,
                    ReportingUserId = userId,
                };

                _context.ReportedUsers.Add(newReport);
                await _context.SaveChangesAsync();

                return new BasicErrorResponse() { ErrorCode = ErrorCodes.None, Success = true, ErrorMessage = "Sucess" };
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to report user. UserId: {userId} Model: {model} Method: {nameof(RequestReportUser)}");
                return new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"Error occured: {ex.Message}", Success = false };
            }
        }
    }
}
