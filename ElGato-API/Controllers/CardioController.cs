using ElGato_API.Data.JWT;
using ElGato_API.Interfaces;
using ElGato_API.VM.Cardio;
using ElGato_API.VMO.Cardio;
using ElGato_API.VMO.ErrorResponse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace ElGato_API.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class CardioController : Controller
    {
        private readonly IJwtService _jwtService;
        private readonly ICardioService _cardioService;
        public CardioController(IJwtService jwtService, ICardioService cardioService)
        {
            _jwtService = jwtService;
            _cardioService = cardioService;
        }

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(List<ChallengeVMO>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]

        public async Task<IActionResult> GetActivChallenges()
        {
            try
            {
                var userId = _jwtService.GetUserIdClaim();

                var res = await _cardioService.GetActiveChallenges(userId);
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

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(List<ActiveChallengeVMO>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]

        public async Task<IActionResult> GetCurrentUserActiveChallanges()
        {
            try
            {
                var userId = _jwtService.GetUserIdClaim();

                var res = await _cardioService.GetUserActiveChallenges(userId);
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

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(CardioTrainingDayVMO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetTrainingDay(DateTime date)
        {
            try
            {
                var userId = _jwtService.GetUserIdClaim();
                var res = await _cardioService.GetTrainingDay(userId, date);
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
            catch(Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured: {ex.Message}", Success = false });
            }
        }

        [HttpPost]
        [Authorize(Policy = "user")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AddExerciseToTrainingDay([FromBody] AddCardioExerciseVM model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return StatusCode(400, new BasicErrorResponse()
                    {
                        ErrorMessage = $"Model state not valid. Please check {nameof(AddCardioExerciseVM)}",
                        Success = false,
                        ErrorCode = ErrorCodes.ModelStateNotValid,
                    });
                }

                var userId = _jwtService.GetUserIdClaim();

                var res = await _cardioService.AddExerciseToTrainingDay(userId, model);
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
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured: {ex.Message}", Success = false });
            }
        }

        [HttpPost]
        [Authorize(Policy = "user")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> JoinChallenge(int challengeId)
        {
            try
            {
                if(challengeId == 0)
                {
                    return BadRequest(new BasicErrorResponse() { ErrorCode = ErrorCodes.ModelStateNotValid, ErrorMessage = "Model state not valid.",Success = false });
                }

                var userId = _jwtService.GetUserIdClaim();
                var res = await _cardioService.JoinChallenge(userId, challengeId);
                if (!res.Success)
                {
                    return res.ErrorCode switch
                    {
                        ErrorCodes.Internal => StatusCode(500, res),
                        ErrorCodes.NotFound => NotFound(res),
                        _ => BadRequest(res)
                    };
                }
                
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured: {ex.Message}", Success = false });
            }
        }

        [HttpDelete]
        [Authorize(Policy = "user")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteExercisesFromCardioTrainingDay([FromBody] DeleteExercisesFromCardioTrainingVM model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return StatusCode(400, new BasicErrorResponse() { Success = false, ErrorMessage = $"Model state not valid. Check {nameof(DeleteExercisesFromCardioTrainingVM)}", ErrorCode = ErrorCodes.ModelStateNotValid }); ;
                }

                var userId = _jwtService.GetUserIdClaim();

                var res = await _cardioService.DeleteExercisesFromCardioTrainingDay(userId, model);
                if (!res.Success)
                {
                    return res.ErrorCode switch
                    {
                        ErrorCodes.Internal => StatusCode(500, res),
                        ErrorCodes.NotFound => NotFound(res),
                        _ => BadRequest(res)
                    };
                }

                return Ok();
            }
            catch(Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = ex.Message, Success = false });
            }
        }
    }
}
