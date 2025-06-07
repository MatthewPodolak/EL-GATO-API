using ElGato_API.Data.JWT;
using ElGato_API.Interfaces;
using ElGato_API.VM.Community;
using ElGato_API.VMO.Community;
using ElGato_API.VMO.ErrorResponse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;

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
        [ProducesResponseType(typeof(UserSearchVMO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SearchForUsers(string query, int limit = 10)
        {
            try
            {
                if (string.IsNullOrEmpty(query))
                {
                    return StatusCode(400, new BasicErrorResponse()
                    {
                        ErrorCode = ErrorCodes.ModelStateNotValid,
                        ErrorMessage = "Query is empty. Provide at least one char.",
                        Success = false
                    });
                }

                var userId = _jwtService.GetUserIdClaim();

                var res = await _communityService.SearchForUsers(userId, query, limit);
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
            catch(Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured {ex.Message}", Success = false });
            }
        }

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(UserProfileDataVMO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetUserProfile(string? userId)
        {
            try
            {
                var askingUserId = _jwtService.GetUserIdClaim();

                if(string.IsNullOrEmpty(userId))
                {
                    var res = await _communityService.GetUserProfileData(askingUserId, askingUserId);
                    if (!res.error.Success)
                    {
                        return res.error.ErrorCode switch
                        {
                            ErrorCodes.Internal => StatusCode(500, res.error),
                            ErrorCodes.NotFound => StatusCode(404, res.error),
                            _ => BadRequest(res.error)
                        };
                    }

                    return Ok(res.data);
                }

                bool canUserAcessFullData = true;

                var isAcessible = await _communityService.CheckIfProfileIsAcessibleForUser(askingUserId, userId);
                if (!isAcessible.Acessible)
                {
                    switch (isAcessible.UnacessilibityReason)
                    {
                        case UnacessilibityReason.Other:
                            return StatusCode(403, new BasicErrorResponse() { ErrorCode = ErrorCodes.Forbidden, ErrorMessage = "Acess forbidden.", Success = false });
                        case UnacessilibityReason.Blocked:
                            return StatusCode(403, new BasicErrorResponse() { ErrorCode = ErrorCodes.Forbidden, ErrorMessage = "Acess forbidden.", Success = false });
                        case UnacessilibityReason.Private:
                            canUserAcessFullData = false;
                            break;
                    }
                }

                var response = await _communityService.GetUserProfileData(userId, askingUserId, canUserAcessFullData);
                if (!response.error.Success)
                {
                    return response.error.ErrorCode switch
                    {
                        ErrorCodes.Internal => StatusCode(500, response.error),
                        ErrorCodes.NotFound => StatusCode(404, response.error),
                        _ => BadRequest(response.error)
                    };
                }

                return Ok(response.data);
            }
            catch(Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured {ex.Message}", Success = false });
            }
        }

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(UserFollowersVMO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetUserFollowers(string? userId = null, bool? onlyFollowed = false)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    userId = _jwtService.GetUserIdClaim();
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
                    if (!canAcess.Acessible)
                    {
                        return StatusCode(403, new BasicErrorResponse()
                        {
                            ErrorCode = ErrorCodes.Forbidden,
                            ErrorMessage = $"Acess forbidden - {canAcess.UnacessilibityReason}",
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

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(FollowersRequestVMO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetFollowersRequests()
        {
            try
            {
                var userId = _jwtService.GetUserIdClaim();

                var res = await _communityService.GetFollowersRequests(userId);
                if (!res.erro.Success)
                {
                    return res.erro.ErrorCode switch
                    {
                        ErrorCodes.NotFound => NotFound(res.erro),
                        ErrorCodes.Internal => StatusCode(500, res.erro),
                        _ => BadRequest(res.erro)
                    };
                }

                return Ok(res.data);
            }
            catch(Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorCode = ErrorCodes.Internal, ErrorMessage = $"An internal server error occured {ex.Message}", Success = false });
            }
        }

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(FriendsLeaderboardVMO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetFriendsLeaderboards()
        {
            try
            {
                var userId = _jwtService.GetUserIdClaim();

                var res = await _communityService.GetFriendsLeaderboards(userId);
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
        public async Task<IActionResult> RespondToFollowRequest([FromBody] RespondToFollowVM model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return StatusCode(400, new BasicErrorResponse()
                    {
                        ErrorCode = ErrorCodes.ModelStateNotValid,
                        ErrorMessage = $"Model state not valid. Please check {nameof(RespondToFollowRequest)}",
                        Success = false
                    });
                }

                var userId = _jwtService.GetUserIdClaim();

                var res = await _communityService.RespondToFollowRequest(userId, model);
                if (!res.Success)
                {
                    return res.ErrorCode switch
                    {
                        ErrorCodes.NotFound => NotFound(res),
                        ErrorCodes.Internal => StatusCode(500, res),
                        ErrorCodes.Forbidden => StatusCode(403, res),
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
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
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

                var acessilibityCheck = await _communityService.CheckIfProfileIsAcessibleForUser(userId, userToFollowId);
                if (!acessilibityCheck.Acessible)
                {
                    switch (acessilibityCheck.UnacessilibityReason)
                    {
                        case UnacessilibityReason.Other:
                            return StatusCode(403, new BasicErrorResponse()
                            {
                                ErrorCode = ErrorCodes.Forbidden,
                                ErrorMessage = "Cannot perform that action.",
                                Success = false
                            });
                        case UnacessilibityReason.Blocked:
                            return StatusCode(403, new BasicErrorResponse()
                            {
                                ErrorCode = ErrorCodes.Forbidden,
                                ErrorMessage = "User is blocked.",
                                Success = false
                            });
                        case UnacessilibityReason.Private:
                            var request = await _communityService.RequestFollow(userId, userToFollowId);
                            if (!request.Success)
                            {
                                return request.ErrorCode switch
                                {
                                    ErrorCodes.NotFound => NotFound(request),
                                    ErrorCodes.Internal => StatusCode(500, request),
                                    _ => BadRequest(request)
                                };
                            }

                            return Ok("Requested");
                    }
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

                return Ok("Followed");
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