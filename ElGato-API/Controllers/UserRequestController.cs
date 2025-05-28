using ElGato_API.Data.JWT;
using ElGato_API.Interfaces;
using ElGato_API.VM.Requests;
using ElGato_API.VMO.ErrorResponse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElGato_API.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class UserRequestController : Controller
    {
        private readonly IJwtService _jwtService;
        private readonly IUserRequestService _requestService;
        public UserRequestController(IJwtService jwtService, IUserRequestService requestService)
        {
            _jwtService = jwtService;
            _requestService = requestService;
        }

        [HttpPost]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ReportIngredientRequest([FromBody] IngredientReportRequestVM model)
        {
            try
            {
                string userId = _jwtService.GetUserIdClaim();

                if (!ModelState.IsValid)
                {
                    return StatusCode(400, new BasicErrorResponse()
                    {
                        ErrorCode = ErrorCodes.ModelStateNotValid,
                        Success = false,
                        ErrorMessage = "Model state not valid."
                    });
                }

                var res = await _requestService.RequestReportIngredient(userId, model);
                if (!res.Success)
                {
                    return res.ErrorCode switch
                    {
                        ErrorCodes.Internal => StatusCode(500, res),
                        _ => BadRequest(res)
                    };
                }                  

                return Ok(res);
            }
            catch (Exception ex) 
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured {ex.Message}", Success = false });
            }
        }

        [HttpPost]
        [Authorize(Policy = "user")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ReportMealRequest([FromBody] ReportMealRequestVM model)
        {
            try
            {
                string userId = _jwtService.GetUserIdClaim();
                if (!ModelState.IsValid)
                {
                    return StatusCode(400, new BasicErrorResponse()
                    {
                        ErrorCode = ErrorCodes.ModelStateNotValid,
                        Success = false,
                        ErrorMessage = "Model state not valid."
                    });
                }

                var res = await _requestService.RequestReportMeal(userId, model);
                if (!res.Success)
                {
                    return res.ErrorCode switch
                    {
                        ErrorCodes.Internal => StatusCode(500, res),
                        _ => BadRequest(res)
                    };
                }

                return Ok();

            }catch (Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured {ex.Message}", Success = false });
            }

        }

        [HttpPost]
        [Authorize(Policy = "user")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ReportUser([FromBody] ReportUserVM model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return StatusCode(400, new BasicErrorResponse()
                    {
                        ErrorCode = ErrorCodes.ModelStateNotValid,
                        Success = false,
                        ErrorMessage = "Model state not valid."
                    });
                }

                var userId = _jwtService.GetUserIdClaim();

                var res = await _requestService.RequestReportUser(userId, model);
                if (!res.Success)
                {
                    return res.ErrorCode switch
                    {
                        ErrorCodes.Internal => StatusCode(500, res),
                        _ => BadRequest(res)
                    };
                }

                return Ok();
            }
            catch(Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured {ex.Message}", Success = false });
            }
        }

        [HttpPost]
        [Authorize(Policy = "user")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AddIngredientRequest([FromBody] AddProductRequestVM model)
        {
            try
            {
                string userId = _jwtService.GetUserIdClaim();

                if (!ModelState.IsValid)
                {
                    return StatusCode(400, new BasicErrorResponse()
                    {
                        ErrorCode = ErrorCodes.ModelStateNotValid,
                        Success = false,
                        ErrorMessage = "Model state not valid."
                    });
                }

                var res = await _requestService.RequestAddIngredient(userId, model);
                if (!res.Success)
                {
                    return res.ErrorCode switch
                    {
                        ErrorCodes.Internal => StatusCode(500, res),
                        _ => BadRequest(res)
                    };
                }

                return Ok();
            }
            catch (Exception ex) 
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured {ex.Message}", Success = false });
            }
        }
    }
}
