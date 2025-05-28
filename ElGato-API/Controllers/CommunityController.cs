using ElGato_API.Data.JWT;
using ElGato_API.Interfaces;
using ElGato_API.VMO.Community;
using ElGato_API.VMO.ErrorResponse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElGato_API.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class CommunityController : Controller
    {
        private readonly IJwtService _jwtService;
        private readonly ICommunityService _communityService;

        public CommunityController(IJwtService jwtService, ICommunityService communityService)
        {
            _jwtService = jwtService;
            _communityService = communityService;
        }

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(UserFollowersVMO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetUserFollowers(string userId, bool? onlyFollowed = false)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return StatusCode(400, new BasicErrorResponse()
                    {
                        ErrorCode = ErrorCodes.ModelStateNotValid,
                        ErrorMessage = "Provide id of user.",
                        Success = false
                    });
                }

                var userExists = await _communityService.UserExists(userId);
                if (!userExists)
                {
                    return NotFound(new BasicErrorResponse()
                    {
                        ErrorCode = ErrorCodes.NotFound,
                        Success = false,
                        ErrorMessage = $"User with id: {userId} does not exists."
                    });
                }

                var askingUserId = _jwtService.GetUserIdClaim();
                if(askingUserId != userId)
                {
                    var canAcess = await _communityService.CheckIfProfileIsAcessibleForUser(userId, askingUserId);
                    if (!canAcess)
                    {
                        return StatusCode(403, new BasicErrorResponse()
                        {
                            ErrorCode = ErrorCodes.Forbidden,
                            ErrorMessage = "Acess forbidden.",
                            Success = false
                        });
                    }
                }

                var res = await _communityService.GetUserFollowerLists(userId, onlyFollowed??false);
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
        [ProducesResponseType(typeof(BlockListVMO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetBlockList()
        {
            try
            {
                var userId = _jwtService.GetUserIdClaim();

                var res = await _communityService.GetUserBlockList(userId);
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
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured {ex.Message}", Success = false });
            }
        }


        [HttpPost]
        [Authorize(Policy = "user")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> FollowUser(string userToFollowId)
        {
            try
            {
                if (string.IsNullOrEmpty(userToFollowId)) 
                {
                    return StatusCode(400, new BasicErrorResponse()
                    {
                        ErrorCode = ErrorCodes.ModelStateNotValid,
                        ErrorMessage = "provide id of user to follow.",
                        Success = false
                    });
                }

                var userId = _jwtService.GetUserIdClaim();
                if(userId == userToFollowId) { return StatusCode(403, new BasicErrorResponse() { ErrorCode = ErrorCodes.Forbidden, ErrorMessage = "Can't follow urslf.", Success = false }); }

                var blockCheckTasks = new List<Task<bool>>
                {
                    _communityService.CheckIfUserIsBlockedBy(userId, userToFollowId),
                    _communityService.CheckIfUserIsBlocking(userId, userToFollowId),
                };
                var blockedCheckResult = await Task.WhenAll(blockCheckTasks);

                if(blockedCheckResult[0] || blockedCheckResult[1])
                {
                    return StatusCode(403, new BasicErrorResponse()
                    {
                        ErrorCode = ErrorCodes.Forbidden,
                        ErrorMessage = "User is blocked.",
                        Success = false
                    });
                }

                var res = await _communityService.FollowUser(userId, userToFollowId);
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
            catch(Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured {ex.Message}", Success = false });
            }
        }

        [HttpPost]
        [Authorize(Policy = "user")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UnFollowUser(string userToUnfollowId)
        {
            try
            {
                if (string.IsNullOrEmpty(userToUnfollowId))
                {
                    return StatusCode(400, new BasicErrorResponse()
                    {
                        ErrorCode = ErrorCodes.ModelStateNotValid,
                        ErrorMessage = "provide id of user to follow.",
                        Success = false
                    });
                }

                var userId = _jwtService.GetUserIdClaim();
                if (userId == userToUnfollowId) { return StatusCode(403, new BasicErrorResponse() { ErrorCode = ErrorCodes.Forbidden, ErrorMessage = "Can't unfollow urslf.", Success = false }); }

                var blockCheckTasks = new List<Task<bool>>
                {
                    _communityService.CheckIfUserIsBlockedBy(userId, userToUnfollowId),
                    _communityService.CheckIfUserIsBlocking(userId, userToUnfollowId),
                };
                var blockedCheckResult = await Task.WhenAll(blockCheckTasks);

                if (blockedCheckResult[0] || blockedCheckResult[1])
                {
                    return StatusCode(403, new BasicErrorResponse()
                    {
                        ErrorCode = ErrorCodes.Forbidden,
                        ErrorMessage = "User is blocked.",
                        Success = false
                    });
                }

                var res = await _communityService.UnFollowUser(userId, userToUnfollowId);
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
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured {ex.Message}", Success = false });
            }
        }

        [HttpPost]
        [Authorize(Policy = "user")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> BlockUser(string userToBlockId)
        {
            try
            {
                if (string.IsNullOrEmpty(userToBlockId))
                {
                    return StatusCode(400, new BasicErrorResponse()
                    {
                        ErrorCode = ErrorCodes.ModelStateNotValid,
                        ErrorMessage = "provide id of user to follow.",
                        Success = false
                    });
                }

                var userId = _jwtService.GetUserIdClaim();
                if(userId == userToBlockId) { return StatusCode(403, new BasicErrorResponse() { ErrorCode = ErrorCodes.Forbidden, ErrorMessage = $"Can't block yourself.", Success = false }); }

                var res = await _communityService.BlockUser(userId, userToBlockId);
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
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured {ex.Message}", Success = false });
            }
        }

        [HttpPost]
        [Authorize(Policy = "user")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UnblockUser(string userToUnblockId)
        {
            try
            {
                if (string.IsNullOrEmpty(userToUnblockId))
                {
                    return StatusCode(400, new BasicErrorResponse()
                    {
                        ErrorCode = ErrorCodes.ModelStateNotValid,
                        ErrorMessage = "provide id of user to follow.",
                        Success = false
                    });
                }

                var userId = _jwtService.GetUserIdClaim();
                if (userId == userToUnblockId) { return StatusCode(403, new BasicErrorResponse() { ErrorCode = ErrorCodes.Forbidden, ErrorMessage = $"Can't unlblock yourself.", Success = false }); }

                var res = await _communityService.UnBlockUser(userId, userToUnblockId);
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
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured {ex.Message}", Success = false });
            }
        }
    }
}