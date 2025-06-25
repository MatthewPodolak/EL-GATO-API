using ElGato_API.Data.JWT;
using ElGato_API.Interfaces;
using ElGato_API.Interfaces.Orchesters;
using ElGato_API.VM.Cardio;
using ElGato_API.VMO.Achievments;
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
        private readonly IAchievmentService _achievmentService;
        private readonly ICardioOrchester _cardioOrchester;
        public CardioController(IJwtService jwtService, ICardioService cardioService, IAchievmentService achievmentService, ICardioOrchester cardioOrchester)
        {
            _jwtService = jwtService;
            _cardioService = cardioService;
            _achievmentService = achievmentService;
            _cardioOrchester = cardioOrchester;
        }

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(List<ChallengeVMO>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]

        public async Task<IActionResult> GetActivChallenges()
        {
            try
            {
                var userId = _jwtService.GetUserIdClaim();

                var res = await _achievmentService.GetActiveChallenges(userId);
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
                return StatusCode(500, ErrorResponse.Internal(ex.Message));
            }
        }

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(List<ActiveChallengeVMO>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]

        public async Task<IActionResult> GetCurrentUserActiveChallanges()
        {
            try
            {
                var userId = _jwtService.GetUserIdClaim();

                var res = await _achievmentService.GetUserActiveChallenges(userId);
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
                return StatusCode(500, ErrorResponse.Internal(ex.Message));
            }
        }

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(CardioTrainingDayVMO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
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
                return StatusCode(500, ErrorResponse.Internal(ex.Message));
            }
        }

        [HttpPost]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(AchievmentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AddExerciseToTrainingDay([FromBody] AddCardioExerciseVM model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return StatusCode(400, ErrorResponse.StateNotValid<AddCardioExerciseVM>());
                }

                var userId = _jwtService.GetUserIdClaim();

                var res = await _cardioOrchester.AddExerciseToTrainingDay(userId, model);
                if (!res.Status.Success)
                {
                    return res.Status.ErrorCode switch
                    {
                        ErrorCodes.NotFound => NotFound(res),
                        ErrorCodes.Internal => StatusCode(500, res),
                        _ => BadRequest(res)
                    };
                }

                return Ok(res.Status);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ErrorResponse.Internal(ex.Message));
            }
        }

        [HttpPost]
        [Authorize(Policy = "user")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> JoinChallenge(int challengeId)
        {
            try
            {
                if(challengeId == 0)
                {
                    return StatusCode(400, ErrorResponse.StateNotValid<int>());
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
                return StatusCode(500, ErrorResponse.Internal(ex.Message));
            }
        }

        [HttpPatch]
        [Authorize(Policy = "user")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ChangeExerciseVisilibity([FromBody] ChangeExerciseVisilibityVM model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return StatusCode(400, ErrorResponse.StateNotValid<ChangeExerciseVisilibityVM>());
                }

                var userId = _jwtService.GetUserIdClaim();
                var res = await _cardioService.ChangeExerciseVisilibity(userId, model);

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
                return StatusCode(500, ErrorResponse.Internal(ex.Message));
            }
        }

        [HttpDelete]
        [Authorize(Policy = "user")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteExercisesFromCardioTrainingDay([FromBody] DeleteExercisesFromCardioTrainingVM model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return StatusCode(400, ErrorResponse.StateNotValid<DeleteExercisesFromCardioTrainingVM>());
                }

                var userId = _jwtService.GetUserIdClaim();

                var res = await _cardioOrchester.DeleteExercisesFromCardioTrainingDay(userId, model);
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
                return StatusCode(500, ErrorResponse.Internal(ex.Message));
            }
        }
    }
}
