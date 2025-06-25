using ElGato_API.Data;
using ElGato_API.Data.JWT;
using ElGato_API.Interfaces;
using ElGato_API.VM;
using ElGato_API.VM.User_Auth;
using ElGato_API.VMO.ErrorResponse;
using ElGato_API.VMO.User;
using ElGato_API.VMO.UserAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace ElGato_API.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class AccountController : Controller
    {
        private readonly IAccountService _accountService;
        private readonly IDietService _dietService;
        private readonly IMongoInits _mongoInits;
        private readonly IJwtService _jwtService;

        public AccountController(IAccountService accountService, IDietService dietService, IMongoInits mongoInits, IJwtService jwtService)
        {
            _accountService = accountService;
            _dietService = dietService;
            _mongoInits = mongoInits;
            _jwtService = jwtService;
        }

        /// <summary>
        /// Handles user registration along with a questionary submission. 
        /// </summary>
        /// <param name="registerVM">An object containing registration creds and questionary information.</param>
        /// <remarks>
        /// This method allows anonymous access and handles both user registration along with questionary processing and saving, registration will fail if the questionary data is invalid.
        /// </remarks>
        [HttpPost]
        [AllowAnonymous]
        [ProducesResponseType(typeof(RegisterVMO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RegisterWithQuestionary([FromBody]RegisterWithQuestVM registerVM) 
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ErrorResponse.StateNotValid<RegisterWithQuestVM>());
            }

            RegisterVMO registerVMO = new RegisterVMO();

            try
            {
                var mailStatus = await _accountService.IsEmailAlreadyUsed(registerVM.Email);
                if (mailStatus)
                {
                    return StatusCode(409, ErrorResponse.AlreadyExists("Account with given E-mail address already exists"));
                }
                    
                var calorieIntake = _dietService.CalculateCalories(registerVM.Questionary);
                registerVMO.calorieIntake = calorieIntake;

                var res = await _accountService.RegisterUser(registerVM, calorieIntake);
                if (!res.Succeeded) 
                {
                    registerVMO.Errors = res.Errors; 
                    registerVMO.Success = false; 

                    return StatusCode(400, ErrorResponse.Failed(res.Errors.ToString()) ); 
                }

                var loginRes = await _accountService.LoginUser(new LoginVM() { Email = registerVM.Email, Password = registerVM.Password });
                if (!loginRes.IdentityResult.Succeeded) 
                {
                    registerVMO.Success = false;

                    return StatusCode(400, ErrorResponse.Failed(res.Errors.ToString()));
                }

                registerVMO.Success = true;
                registerVMO.JWT = loginRes.JwtToken;

                await _mongoInits.CreateUserDietDocument(_jwtService.GetUserIdClaimStringBased(loginRes.JwtToken));
                await _mongoInits.CreateUserTrainingDocument(_jwtService.GetUserIdClaimStringBased(loginRes.JwtToken));
                await _mongoInits.CreateUserExerciseHistoryDocument(_jwtService.GetUserIdClaimStringBased(loginRes.JwtToken));

                return Ok(registerVMO);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ErrorResponse.Internal(ex.Message));
            }
        }

        /// <summary>
        /// Handles user loging in.
        /// </summary>
        /// <param name="loginVM">An object containing login creds</param>
        [HttpPost]
        [AllowAnonymous]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Login([FromBody] LoginVM loginVM)
        {
            if (!ModelState.IsValid)
            {
                return StatusCode(400, ErrorResponse.StateNotValid<LoginVM>());
            }

            try
            {
                var loginResponse = await _accountService.LoginUser(loginVM);

                if (loginResponse.IdentityResult.Succeeded)
                    return Ok(new { token = loginResponse.JwtToken });

                return StatusCode(400, ErrorResponse.Failed(loginResponse.IdentityResult.Errors.ToString()));

            }
            catch (Exception ex) 
            {
                return StatusCode(500, ErrorResponse.Internal(ex.Message));
            }
        }

        [HttpPatch]
        public async Task<IActionResult> ChangePassword() 
        { 
            throw new NotImplementedException();
        }

        [HttpPatch]
        public async Task<IActionResult> ChangeEmail()
        {
            throw new NotImplementedException();
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteAccount()
        {
            throw new NotImplementedException();
        }
    }
}
