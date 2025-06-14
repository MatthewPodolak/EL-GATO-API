using ElGato_API.Data.JWT;
using ElGato_API.Interfaces;
using ElGato_API.VM.Meal;
using ElGato_API.VMO.Achievments;
using ElGato_API.VMO.ErrorResponse;
using ElGato_API.VMO.Meals;
using ElGato_API.VMO.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using MongoDB.Bson;

namespace ElGato_API.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class MealController : Controller
    {
        private readonly IJwtService _jwtService;
        private readonly IMealService _mealService;
        public MealController(IJwtService jwtService, IMealService mealService)
        {
            _mealService = mealService;
            _jwtService = jwtService;
        }

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(StartersVMO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetStarters()
        {
            try
            {
                List<string> UserLikes = new List<string>();
                List<string> UserSaves = new List<string>();

                string userId = _jwtService.GetUserIdClaim();
                var userLikesDoc = await _mealService.GetUserMealLikeDoc(userId);

                if (userLikesDoc.error.Success) {
                    UserLikes = userLikesDoc.res.LikedMeals;
                    UserSaves = userLikesDoc.res.SavedMeals;
                }

                var tasks = new[]
                {
                    _mealService.GetByMainCategory(userId, UserLikes, UserSaves, "Breakfast", 5),
                    _mealService.GetByMainCategory(userId,UserLikes, UserSaves, "Main", 5),
                    _mealService.GetByMainCategory(userId,UserLikes, UserSaves, "Side", 5),
                    _mealService.GetByHighMakro(userId,UserLikes, UserSaves, "protein", 5),
                    _mealService.GetByHighMakro(userId,UserLikes, UserSaves, "carbs", 5),
                    _mealService.GetByLowMakro(userId,UserLikes, UserSaves, "carbs", 5),
                    _mealService.GetByLowMakro(userId,UserLikes, UserSaves, "fats", 5),
                    _mealService.GetMostLiked(userId,UserLikes, UserSaves, 5),
                    _mealService.GetRandom(userId,UserLikes, UserSaves, 5)
                };

                var results = await Task.WhenAll(tasks);

                foreach (var (res, error) in results)
                {
                    if (!error.Success)
                    {
                        return error.ErrorCode switch
                        {
                            ErrorCodes.Internal => StatusCode(500, error),
                            _ => BadRequest(error),
                        };
                    }
                }

                StartersVMO starters = new StartersVMO()
                {
                    Breakfast = results[0].res,
                    MainDish = results[1].res,
                    SideDish = results[2].res,
                    HighProtein = results[3].res,
                    HighCarb = results[4].res,
                    LowCarbs = results[5].res,
                    LowFats = results[6].res,
                    MostLiked = results[7].res,
                    All = results[8].res,
                };

                return Ok(starters);
            }
            catch (Exception ex) 
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorMessage= $"Internal server error: {ex.Message}", ErrorCode = ErrorCodes.Internal, Success = false });
            }
        }

        [HttpPost]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(List<SimpleMealVMO>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetExtendedStarters(ExtendedStartersVM model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return StatusCode(400, new BasicErrorResponse()
                    {
                        Success = false,
                        ErrorMessage = "Modal state not valid",
                        ErrorCode = ErrorCodes.ModelStateNotValid,
                    });
                }

                List<string> UserLikes = new List<string>();
                List<string> UserSaves = new List<string>();

                string userId = _jwtService.GetUserIdClaim();
                var userLikesDoc = await _mealService.GetUserMealLikeDoc(userId);

                if (userLikesDoc.error.Success)
                {
                    UserLikes = userLikesDoc.res.LikedMeals;
                    UserSaves = userLikesDoc.res.SavedMeals;
                }

                List<SimpleMealVMO> mealList = new List<SimpleMealVMO>();
                switch (model.Type)
                {
                    case "Most Liked":
                        var likedMealRes = await _mealService.GetMostLiked(userId, UserLikes, UserSaves, model.pageSize, model.page);
                        if (!likedMealRes.error.Success)
                            return StatusCode(400, likedMealRes.error);
                        mealList.AddRange(likedMealRes.res);
                        break;
                    case "All":
                        var rndMealRes = await _mealService.GetRandom(userId, UserLikes, UserSaves, model.pageSize, model.page);
                        if (!rndMealRes.error.Success)
                            return StatusCode(400, rndMealRes.error);
                        mealList.AddRange(rndMealRes.res);
                        break;

                    case "Breakfast":
                        var breakfastRes = await _mealService.GetByMainCategory(userId, UserLikes, UserSaves, "Breakfast", model.pageSize, model.page);
                        if (!breakfastRes.error.Success)
                            return StatusCode(400, breakfastRes.error);
                        mealList.AddRange(breakfastRes.res);
                        break;

                    case "Side Dish":
                        var sideDishRes = await _mealService.GetByMainCategory(userId, UserLikes, UserSaves, "Side", model.pageSize, model.page);
                        if (!sideDishRes.error.Success)
                            return StatusCode(400, sideDishRes.error);
                        mealList.AddRange(sideDishRes.res);
                        break;

                    case "Main Dish":
                        var mainDishRes = await _mealService.GetByMainCategory(userId, UserLikes, UserSaves, "Main", model.pageSize, model.page);
                        if (!mainDishRes.error.Success)
                            return StatusCode(400, mainDishRes.error);
                        mealList.AddRange(mainDishRes.res);
                        break;

                    case "High Protein":
                        var highProteinRes = await _mealService.GetByHighMakro(userId, UserLikes, UserSaves, "protein", model.pageSize, model.page);
                        if (!highProteinRes.error.Success)
                            return StatusCode(400, highProteinRes.error);
                        mealList.AddRange(highProteinRes.res);
                        break;

                    case "Low Carbs":
                        var lowCarbsRes = await _mealService.GetByLowMakro(userId, UserLikes, UserSaves, "carbs", model.pageSize, model.page);
                        if (!lowCarbsRes.error.Success)
                            return StatusCode(400, lowCarbsRes.error);
                        mealList.AddRange(lowCarbsRes.res);
                        break;

                    case "High Carbs":
                        var highCarbsRes = await _mealService.GetByHighMakro(userId, UserLikes, UserSaves, "carbs", model.pageSize, model.page);
                        if (!highCarbsRes.error.Success)
                            return StatusCode(400, highCarbsRes.error);
                        mealList.AddRange(highCarbsRes.res);
                        break;

                    case "Low Fat":
                        var lowFatRes = await _mealService.GetByLowMakro(userId, UserLikes, UserSaves, "fats", model.pageSize, model.page);
                        if (!lowFatRes.error.Success)
                            return StatusCode(400, lowFatRes.error);
                        mealList.AddRange(lowFatRes.res);
                        break;

                    default:
                        return BadRequest(new BasicErrorResponse()
                        {
                            ErrorCode = ErrorCodes.Failed,
                            ErrorMessage = "Invalid type specified.",
                            Success = false
                        });
                };

                return Ok(mealList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorMessage = $"Internal server error: {ex.Message}", ErrorCode = ErrorCodes.Internal, Success = false });
            }

        }

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(List<SimpleMealVMO>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetLikedMeals()
        {
            try
            {
                string userId = _jwtService.GetUserIdClaim();
                List<SimpleMealVMO> res = new List<SimpleMealVMO>();
                var tasks = new[]
                {
                    _mealService.GetUserLikedMeals(userId),
                    _mealService.GetUserSavedMeals(userId),
                };

                var results = await Task.WhenAll(tasks);

                foreach (var (error, ress) in results)
                {
                    if (!error.Success)
                    {
                        return error.ErrorCode switch
                        {
                            ErrorCodes.Internal => StatusCode(500, error),
                            ErrorCodes.NotFound => NotFound(error),
                            _ => BadRequest(error)
                        };
                    }
                }

                HashSet<SimpleMealVMO> uniqueMeals = new HashSet<SimpleMealVMO>(results[0].res);
                uniqueMeals.UnionWith(results[1].res);

                return Ok(uniqueMeals.ToList());
            }
            catch (Exception ex) 
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorMessage = $"Internal server error. {ex.Message}", ErrorCode = ErrorCodes.Internal });
            }
        }

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(List<SimpleMealVMO>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetOwnRecipes()
        {
            try
            {
                List<string> UserLikes = new List<string>();
                List<string> UserSaves = new List<string>();

                string userId = _jwtService.GetUserIdClaim();
                var userLikesDoc = await _mealService.GetUserMealLikeDoc(userId);

                if (userLikesDoc.error.Success)
                {
                    UserLikes = userLikesDoc.res.LikedMeals;
                    UserSaves = userLikesDoc.res.SavedMeals;
                }

                var res = await _mealService.GetOwnMeals(userId, UserLikes, UserSaves);
                if (!res.error.Success)
                {
                    return res.error.ErrorCode switch
                    {
                        ErrorCodes.Internal => StatusCode(500, res.error),
                        _ => BadRequest(res.error)
                    };
                }

                return Ok(res.res);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorMessage = $"Internal server error. {ex.Message}", ErrorCode = ErrorCodes.Internal });
            }
        }

        [HttpGet]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(List<SimpleMealVMO>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetUserRecipes(string? userId = null, int count = 10, int skip = 0)
        {
            try
            {
                var askingUserId = _jwtService.GetUserIdClaim();
                if (String.IsNullOrEmpty(userId))
                {
                    userId = askingUserId;
                }

                List<string> userLikes = new List<string>();
                List<string> userSaves = new List<string>();

                var userLikesDoc = await _mealService.GetUserMealLikeDoc(userId);
                if (!userLikesDoc.error.Success)
                {
                    return StatusCode(400, userLikesDoc.error);
                }

                userLikes = userLikesDoc.res.LikedMeals;
                userSaves = userLikesDoc.res.SavedMeals;

                var res = await _mealService.GetUserRecipes(userId, count, skip, userLikes, userSaves);
                if (!res.error.Success)
                {
                    return res.error.ErrorCode switch
                    {
                        ErrorCodes.Internal => StatusCode(500, res.error),
                        ErrorCodes.Forbidden => StatusCode(403, res.error),
                        ErrorCodes.NotFound => NotFound(res.error),
                        _ => BadRequest(res.error)
                    };
                }

                return Ok(res.data);
            }
            catch(Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorMessage = $"Internal server error. {ex.Message}", ErrorCode = ErrorCodes.Internal });
            }
        }

        [HttpPost]
        [Authorize(Policy = "user")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> LikeMeal(string mealId)
        {
            try
            {
                string userId = _jwtService.GetUserIdClaim();

                var res = await _mealService.LikeMeal(userId, mealId);
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
                return StatusCode(500, new BasicErrorResponse() { ErrorMessage = $"Internal server error. {ex.Message}", ErrorCode = ErrorCodes.Internal });
            }
        }

        [HttpPost]
        [Authorize(Policy = "user")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SaveMeal(string mealId)
        {
            try
            {
                string userId = _jwtService.GetUserIdClaim();

                var res = await _mealService.SaveMeal(userId, mealId);
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
                return StatusCode(500, new BasicErrorResponse() { ErrorMessage = $"Internal server error. {ex.Message}", ErrorCode = ErrorCodes.Internal });
            }
        }

        [HttpPost]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(List<SimpleMealVMO>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Search(SearchMealVM model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return StatusCode(400, new BasicErrorResponse()
                    {
                        Success = false,
                        ErrorMessage = "Modal state not valid",
                        ErrorCode = ErrorCodes.ModelStateNotValid,
                    });
                }

                string userId = _jwtService.GetUserIdClaim();

                var res = await _mealService.Search(userId, model);
                if (!res.error.Success) 
                {
                    return res.error.ErrorCode switch
                    {
                        ErrorCodes.Internal => StatusCode(500, res.error),
                        _ => BadRequest(res.error)
                    };
                }

                return Ok(res.res);
            }
            catch (Exception ex) 
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorMessage = $"Internal server error. {ex.Message}", ErrorCode = ErrorCodes.Internal });
            }
        }

        [HttpPost]
        [Authorize(Policy = "user")]
        [ProducesResponseType(typeof(AchievmentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> PublishMeal([FromForm]PublishMealVM model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return StatusCode(400, new BasicErrorResponse()
                    {
                        Success = false,
                        ErrorMessage = "Modal state not valid",
                        ErrorCode = ErrorCodes.ModelStateNotValid,
                    });
                }

                string userId = _jwtService.GetUserIdClaim();

                var res = await _mealService.ProcessAndPublishMeal(userId, model);
                if (!res.error.Success) 
                { 
                    return res.error.ErrorCode switch
                    {
                        ErrorCodes.Failed => BadRequest(res.error),
                        ErrorCodes.Internal => StatusCode(500, res.error),
                        _ => BadRequest(res.error),
                    };
                }

                if (res.ach != null) { return Ok(res.ach); }

                return Ok(new AchievmentResponse() { Status = new BasicErrorResponse() { ErrorCode = ErrorCodes.None, Success = true, ErrorMessage = "Sucess" } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorMessage = $"Internal server error. {ex.Message}", ErrorCode = ErrorCodes.Internal });
            }
        }

        [HttpDelete]
        [Authorize(Policy = "user")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BasicErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteMeal(string mealId)
        {
            try
            {
                string userId = _jwtService.GetUserIdClaim();

                ObjectId mealIdObj = ObjectId.Parse(mealId);

                var res = await _mealService.DeleteMeal(userId, mealIdObj);
                if (!res.Success) 
                {
                    return res.ErrorCode switch
                    {
                        ErrorCodes.NotFound => NotFound(res),
                        ErrorCodes.Forbidden => new ObjectResult(res)
                        {
                            StatusCode = StatusCodes.Status403Forbidden
                        },
                        ErrorCodes.Internal => StatusCode(500, res),
                        _ => BadRequest(res)
                    };
                }

                return Ok();
            }
            catch(Exception ex)
            {
                return StatusCode(500, new BasicErrorResponse() { ErrorMessage = $"Internal server error. {ex.Message}", ErrorCode = ErrorCodes.Internal });
            }
        }

    }
}

