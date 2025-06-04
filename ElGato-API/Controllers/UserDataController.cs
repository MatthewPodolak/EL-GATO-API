using ElGato_API.Data;
using ElGato_API.Data.JWT;
using ElGato_API.Interfaces;
using ElGato_API.Models.User;
using ElGato_API.ModelsMongo.Statistics;
using ElGato_API.VM.UserData;
using ElGato_API.VMO.Achievments;
using ElGato_API.VMO.ErrorResponse;
using ElGato_API.VMO.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Transactions;

namespace ElGato_API.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class UserDataController : Controller
    {
        private readonly IJwtService _jwtService;
        private readonly IUserService _userService;
        private readonly IAchievmentService _achievmentService;
        public UserDataController(IJwtService jwtService, IUserService userService, IAchievmentService achievmentService)
        {
            _jwtService = jwtService;
            _userService = userService;
            _achievmentService = achievmentService;
        }

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(UserCalorieIntake), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetUserCaloriesIntake()
        {
            try
            {
                string userId = _jwtService.GetUserIdClaim();

                var res = await _userService.GetUserCalories(userId);
                if (!res.error.Success)
                {
                    return res.error.ErrorCode switch
                    {
                        ErrorCodes.NotFound => NotFound(res.error),
                        ErrorCodes.Internal => StatusCode(500, res.error),
                        _ => BadRequest(res.error)
                    };
                }

                return Ok(res.model);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured {ex.Message}", Success = false });
            }
        }

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(UserCalorieIntake), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetUserCurrentDayCalorieIntake(DateTime date)
        {
            try
            {
                var userId = _jwtService.GetUserIdClaim();

                var res = await _userService.GetCurrentCalories(userId, date);
                if (!res.error.Success)
                {
                    return res.error.ErrorCode switch
                    {
                        ErrorCodes.Internal => StatusCode(500, res.error),
                        ErrorCodes.NotFound => NotFound(res.error),
                        _ => BadRequest(res.error)
                    };
                }

                return Ok(res.model);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured: {ex.Message}", Success = false });
            }
        }

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(double), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetUserWeight()
        {
            try
            {
                var userId = _jwtService.GetUserIdClaim();

                var res = await _userService.GetCurrentUserWeight(userId);
                if (!res.error.Success)
                {
                    return res.error.ErrorCode switch
                    {
                        ErrorCodes.NotFound => NotFound(res.error),
                        ErrorCodes.Internal => StatusCode(500, res.error),
                        _ => BadRequest(res.error)
                    };
                }

                return Ok(res.weight);
            }
            catch(Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured: {ex.Message}", Success = false });
            }
        }

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(double), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetUserWaterIntake(DateTime date)
        {
            try
            {
                var userId = _jwtService.GetUserIdClaim();

                var res = await _userService.GetCurrentWaterIntake(userId, date);
                if (!res.error.Success)
                {
                    return res.error.ErrorCode switch
                    {
                        ErrorCodes.NotFound => NotFound(res.error),
                        ErrorCodes.Internal => StatusCode(500, res.error),
                        _ => BadRequest(res.error)
                    };
                }

                return Ok(res.water);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured: {ex.Message}", Success = false });
            }

        }

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetUserMeasurmentsSystem()
        {
            try
            {
                string userId = _jwtService.GetUserIdClaim();
                var res = await _userService.GetSystem(userId);
                if (!res.error.Success)
                {
                    return res.error.ErrorCode switch
                    {
                        ErrorCodes.NotFound => NotFound(res.error),
                        ErrorCodes.Internal => StatusCode(500, res.error),
                        _ => BadRequest(res.error)
                    };
                }

                return Ok(res.data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured {ex.Message}", Success = false });
            }
        }

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(UserLayoutVMO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetUserLayoutData()
        {
            try
            {
                var userId = _jwtService.GetUserIdClaim();

                var res = await _userService.GetUserLayout(userId);
                if (!res.error.Success)
                {
                    return res.error.ErrorCode switch
                    {
                        ErrorCodes.NotFound => NotFound(res.error),
                        ErrorCodes.Internal => StatusCode(500, res.error),
                        _ => BadRequest(res.error),
                    };
                }

                return Ok(res.data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An inernal server error occured {ex.Message}", Success = true });
            }

        }

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(MakroDataVMO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPastMakroData(string? period)
        {
            try
            {
                var userId = _jwtService.GetUserIdClaim();

                var res = await _userService.GetPastMakroData(userId, period ?? "all");
                if (!res.error.Success)
                {
                    return res.error.ErrorCode switch
                    {
                        ErrorCodes.Internal => StatusCode(500, res.error),
                        _ => BadRequest(res.error)
                    };
                }

                return Ok(res.data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured {ex.Message}", Success = false });
            }
        }

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(DailyMakroDistributionVMO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetCurrentDailyMakroDistribution(DateTime date)
        {
            try
            {
                var userId = _jwtService.GetUserIdClaim();

                var res = await _userService.GetDailyMakroDisturbtion(userId, date);
                if (!res.error.Success)
                {
                    return res.error.ErrorCode switch
                    {
                        ErrorCodes.NotFound => NotFound(res.error),
                        ErrorCodes.Internal => StatusCode(500, res.error),
                        _ => BadRequest(res.error)
                    };
                }

                return Ok(res.data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An inernal server error occured: {ex.Message}", Success = false });
            }
        }

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(MuscleUsageDataVMO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetTrainedMuscleData(string? period)
        {
            try
            {
                var userId = _jwtService.GetUserIdClaim();

                var res = await _userService.GetMuscleUsageData(userId, period ?? "all");
                if (!res.error.Success)
                {
                    return res.error.ErrorCode switch
                    {
                        ErrorCodes.Internal => StatusCode(500, res.error),
                        _ => BadRequest(res.error)
                    };
                }

                return Ok(res.data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured: {ex.Message}", Success = false });
            }
        }

        [HttpPost]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(List<ExercisePastDataVMO>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPastDataForExercises([FromBody] GetPasstExerciseDataVM model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new BasicErrorResponse()
                    {
                        ErrorCode = ErrorCodes.ModelStateNotValid,
                        ErrorMessage = "Model state not valid.",
                        Success = false
                    });
                }

                var userId = _jwtService.GetUserIdClaim();

                var getTasks = model.ExercisesNames.Select(name => _userService.GetPastExerciseData(userId, name));
                var results = await Task.WhenAll(getTasks);

                var errorResult = results.FirstOrDefault(r => !r.error.Success);
                if (errorResult.data != null && !errorResult.error.Success)
                {
                    return errorResult.error.ErrorCode switch
                    {
                        ErrorCodes.NotFound => NotFound(errorResult.error),
                        ErrorCodes.Internal => StatusCode(500, errorResult.error),
                        ErrorCodes.ModelStateNotValid => BadRequest(errorResult.error),
                        _ => BadRequest(errorResult.error)
                    };
                }

                List<ExercisePastDataVMO> vmo = results.Select(result => result.data).Where(data => data != null).ToList();

                return Ok(vmo);

            }
            catch (Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured, ex {ex.Message}", Success = false });
            }
        }

        [HttpPatch]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(AchievmentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateUserHealthConnectData([FromBody] List<UserStatisticsVM> model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return StatusCode(400, new BasicErrorResponse() { ErrorCode = ErrorCodes.ModelStateNotValid, ErrorMessage = $"Model state not valid. Check {nameof(UserStatisticsVM)}", Success = false });
                }

                var filteredList = model.Where(x => x.Type == StatisticType.CaloriesBurnt || x.Type == StatisticType.StepsTaken).ToList();
                if (filteredList.Count == 0)
                {
                    return Ok(new AchievmentResponse() { Status = new BasicErrorResponse() { Success = true, ErrorMessage = "Sucess", ErrorCode = ErrorCodes.None } });
                }

                var userId = _jwtService.GetUserIdClaim();

                var res = await _achievmentService.AddFromHealthConnectToStatisticsAndIncrementAchievments(userId, filteredList);
                if (!res.error.Success)
                {
                    return res.error.ErrorCode switch
                    {
                        ErrorCodes.NotFound => NotFound(res.error),
                        ErrorCodes.Internal => StatusCode(500, res.error),
                        ErrorCodes.ModelStateNotValid => BadRequest(res.error),
                        _ => BadRequest(res.error)
                    };
                }

                return Ok(res.ach);
            }
            catch(Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An inernal server error occured: {ex.Message}", Success = false });
            }
        }

        [HttpPatch]
        [Authorize(Policy = "user")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateUserLayout([FromBody] UserLayoutVM model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return StatusCode(400, new BasicErrorResponse() { ErrorCode = ErrorCodes.ModelStateNotValid, ErrorMessage = $"Model state not valid. Check {nameof(UserLayoutVM)}", Success = false });
                }

                var userId = _jwtService.GetUserIdClaim();

                var res = await _userService.UpdateLayout(userId, model);
                if (!res.Success)
                {
                    return res.ErrorCode switch
                    {
                        ErrorCodes.NotFound => NotFound(res),
                        ErrorCodes.Internal => StatusCode(500, res),
                        _ => BadRequest(res)
                    };
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An inernal server error occured: {ex.Message}", Success = false });
            }

        }
    }
}