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
        public async Task<ErrorResponse> RequestAddIngredient(string userId, AddProductRequestVM model)
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

                return ErrorResponse.Ok();

            }catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to request ingridient addition. UserId: {userId} Data: {model} Method: {nameof(RequestAddIngredient)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<ErrorResponse> RequestReportIngredient(string userId, IngredientReportRequestVM model)
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

                return ErrorResponse.Ok();
            }
            catch (Exception ex) {
                _logger.LogError(ex, $"Failed while trying to report ingredient. UserId: {userId} Data: {model} Method: {nameof(RequestReportIngredient)}");
                return ErrorResponse.Internal(ex.Message);
            }
        
        }

        public async Task<ErrorResponse> RequestReportMeal(string userId, ReportMealRequestVM model)
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

                return ErrorResponse.Ok();
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, $"Failed while trying to report meal. UserId: {userId} Data: {model} Method: {nameof(RequestReportMeal)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<ErrorResponse> RequestReportUser(string userId, ReportUserVM model)
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

                return ErrorResponse.Ok();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to report user. UserId: {userId} Model: {model} Method: {nameof(RequestReportUser)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }
    }
}
