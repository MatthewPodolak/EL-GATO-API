using ElGato_API.Interfaces;
using ElGato_API.Interfaces.Orchesters;
using ElGato_API.VM;
using ElGato_API.VM.User_Auth;
using ElGato_API.VMO.ErrorResponse;
using ElGato_API.VMO.UserAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElGato_API.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class AccountController : Controller
    {
        private readonly IAccountService _accountService;
        private readonly IAccountOrchester _accountOrchester;

        public AccountController(IAccountService accountService, IAccountOrchester accountOrchester)
        {
            _accountService = accountService;
            _accountOrchester = accountOrchester;
        }

        [HttpPost]
        [AllowAnonymous]
        [ProducesResponseType(typeof(RegisterVMO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RegisterVMO), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(RegisterVMO), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(RegisterVMO), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RegisterWithQuestionary([FromBody]RegisterWithQuestVM model) 
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ErrorResponse.StateNotValid<RegisterWithQuestVM>());
            }

            try
            {
                var vmo = new RegisterVMO();

                var mailStatus = await _accountService.IsEmailAlreadyUsed(model.Email);
                if (mailStatus)
                {
                    vmo.ErrorResponse = ErrorResponse.AlreadyExists("E-mail address already exists.");
                    return StatusCode(409, vmo);
                }

                var res = await _accountOrchester.RegisterWithQuestionary(model);
                if (!res.ErrorResponse.Success)
                {
                    return res.ErrorResponse.ErrorCode switch
                    {
                        ErrorCodes.AlreadyExists => StatusCode(409, res),
                        ErrorCodes.Internal => StatusCode(500, res),
                        _ => BadRequest(res)
                    };
                }

                return Ok(res);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ErrorResponse.Internal(ex.Message));
            }
        }


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