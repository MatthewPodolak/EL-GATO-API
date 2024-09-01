﻿using ElGato_API.Interfaces;
using ElGato_API.VM;
using ElGato_API.VM.User_Auth;
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

        public AccountController(IAccountService accountService, IDietService dietService)
        {
            _accountService = accountService;
            _dietService = dietService;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterWithQuestionary([FromBody]RegisterWithQuestVM registerVM) 
        {
            if (!ModelState.IsValid)
                return StatusCode(400, "Model state not valid.");

            RegisterVMO registerVMO = new RegisterVMO();

            try
            {
                var mailStatus = await _accountService.IsEmailAlreadyUsed(registerVM.Email);
                if (mailStatus)
                    return StatusCode(409, "E-mail address already in use");

                var calorieIntake = _dietService.CalculateCalories(registerVM.Questionary);
                registerVMO.calorieIntake = calorieIntake;

                var res = await _accountService.RegisterUser(registerVM, calorieIntake);
                if (!res.Succeeded) { registerVMO.Errors = res.Errors; registerVMO.Success = false; return StatusCode(400, registerVMO); }

                var loginRes = await _accountService.LoginUser(new LoginVM() { Email = registerVM.Email, Password = registerVM.Password });
                if (!loginRes.IdentityResult.Succeeded) {
                    registerVMO.Success = false;
                    return StatusCode(400, registerVMO);
                }

                registerVMO.Success = true;
                registerVMO.JWT = loginRes.JwtToken;

                return Ok(registerVMO);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: Internal server error. {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginVM loginVM)
        {
            if (!ModelState.IsValid)
                return StatusCode(400, "Invalid form send");

            try
            {
                var loginResponse = await _accountService.LoginUser(loginVM);

                if (loginResponse.IdentityResult.Succeeded)
                    return Ok(new { token = loginResponse.JwtToken });

                return StatusCode(400, new { errors = loginResponse.IdentityResult.Errors });

            }
            catch (Exception ex) { 
                return StatusCode(500, ex.Message);
            }        
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword() 
        { 
            throw new NotImplementedException();
        }

    }
}
