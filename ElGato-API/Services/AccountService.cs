using ElGato_API.Data;
using ElGato_API.Data.JWT;
using ElGato_API.Interfaces;
using ElGato_API.Models.User;
using ElGato_API.VM;
using ElGato_API.VM.User_Auth;
using ElGato_API.VMO.ErrorResponse;
using ElGato_API.VMO.UserAuth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ElGato_API.Services
{
    public class AccountService : IAccountService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IJwtService _jwtService;
        private readonly ILogger<AccountService> _logger;
        private readonly IDietService _dietService;

        public AccountService(AppDbContext context, UserManager<AppUser> userManager, IJwtService jwtService, ILogger<AccountService> logger, IDietService dietService)
        {
            _context = context;
            _userManager = userManager;
            _jwtService = jwtService;
            _logger = logger;
            _dietService = dietService;
        }

        public async Task<bool> IsEmailAlreadyUsed(string email)
        {
            try
            {
                var res = await _context.AppUser.FirstOrDefaultAsync(a => a.Email == email);

                if (res != null)
                    return true;

                return false;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Error occurred while checking if email {email} is already used.");
                return true;               
            }
        }

        public async Task<LoginVMO> LoginUser(LoginVM loginVM)
        {
            var user = await _userManager.FindByEmailAsync(loginVM.Email);

            if (user == null)
            {
                _logger.LogWarning("Login failed: Email {Email} not found.", loginVM.Email);
                return CreateFailedLoginResult("E-mail address is invalid");
            }

            var isPasswordValid = await _userManager.CheckPasswordAsync(user, loginVM.Password);
            if (!isPasswordValid)
            {
                _logger.LogWarning("Login failed: Invalid password for email {Email}.", loginVM.Email);
                return CreateFailedLoginResult("Password is invalid");
            }
                
            var userRoles = await _userManager.GetRolesAsync(user);

            var token = _jwtService.GenerateJwtToken(user, loginVM.Email, userRoles);

            _logger.LogInformation("User {Email} logged in successfully.", loginVM.Email);

            return new LoginVMO
            {
                IdentityResult = IdentityResult.Success,
                JwtToken = token
            };
        }

        public async Task<ErrorResponse> RegisterUser(RegisterWithQuestVM model)
        {
            try
            {
                var userInformation = new UserInformation
                {
                    Age = model.Questionary.Age,
                    Weight = model.Questionary.Weight,
                    Height = model.Questionary.Height,
                    TrainingDays = model.Questionary.TrainingDays,
                    Sleep = model.Questionary.Sleep,
                    DailyTimeSpendWorking = model.Questionary.DailyTimeSpendWorking,
                    JobType = model.Questionary.JobType,
                    Woman = model.Questionary.Woman,
                    BodyType = model.Questionary.BodyType,
                    Goal = model.Questionary.Goal,
                };

                var userCalories = _dietService.CalculateCalories(model.Questionary);
                var calories = new CalorieInformation
                {
                    Carbs = userCalories.Carbs,
                    Fat = userCalories.Fat,
                    Kcal = userCalories.Kcal,
                    Protein = userCalories.Protein,
                    Kj = userCalories.Kj,
                };

                var user = new AppUser { UserName = model.Email, Email = model.Email, Name = model.Questionary.Name, UserInformation = userInformation, Metric = model.Questionary.Metric, CalorieInformation = calories };
                var res = await _userManager.CreateAsync(user, model.Password);
                if (res.Succeeded)
                {
                    _logger.LogInformation($"New user registered successfully. {user.Id}");
                    await _userManager.AddToRoleAsync(user, "user");
                    return ErrorResponse.Ok();
                }

                var errors = string.Join(", ", res.Errors.Select(e => e.Description));
                return ErrorResponse.Failed(errors);

            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed to register user.");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        //priv
        private LoginVMO CreateFailedLoginResult(string errorMessage)
        {
            return new LoginVMO
            {
                IdentityResult = IdentityResult.Failed(new IdentityError { Description = errorMessage })
            };
        }
    }
}