﻿using ElGato_API.Data;
using ElGato_API.Interfaces;
using ElGato_API.ModelsMongo.Diet;
using ElGato_API.ModelsMongo.Meal;
using ElGato_API.VM.Meal;
using ElGato_API.VMO.Achievments;
using ElGato_API.VMO.ErrorResponse;
using ElGato_API.VMO.Meals;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Net;

namespace ElGato_API.Services
{
    public class MealService : IMealService
    {
        private readonly IMongoCollection<MealsDocument> _mealsCollection;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly AppDbContext _context;
        private readonly IMongoCollection<MealLikesDocument> _mealLikesCollection;
        private readonly IMongoCollection<OwnMealsDocument> _ownMealCollection;
        private readonly IAchievmentService _achievmentService;
        private readonly IHelperService _helperService;
        private readonly ILogger<MealService> _logger;

        public MealService(IMongoDatabase database, IDbContextFactory<AppDbContext> contextFactory, IAchievmentService achievmentService, ILogger<MealService> logger, IHelperService helperService, AppDbContext context)
        {
            _mealsCollection = database.GetCollection<MealsDocument>("MealsDoc");
            _mealLikesCollection = database.GetCollection<MealLikesDocument>("MealsLikeDoc");
            _ownMealCollection = database.GetCollection<OwnMealsDocument>("OwnMealsDoc");
            _contextFactory = contextFactory;
            _achievmentService = achievmentService;
            _logger = logger;
            _helperService = helperService;
            _context = context;
        }

        public async Task<(List<SimpleMealVMO> res, ErrorResponse error)> GetByMainCategory(string userId, List<string> LikedMeals, List<string> SavedMeals, string? category, int? qty = 5, int? pageNumber = 1)
        {
            List<SimpleMealVMO> res = new List<SimpleMealVMO>();

            try
            {
                using (var _context = _contextFactory.CreateDbContext())
                {
                    if (string.IsNullOrWhiteSpace(category))
                        return (res, ErrorResponse.StateNotValid<string>("Category is required"));

                    var filter = Builders<MealsDocument>.Filter.Regex(meal => meal.Categories, new MongoDB.Bson.BsonRegularExpression(category, "i"));

                    var totalMatchingDocs = await _mealsCollection.CountDocumentsAsync(filter);

                    var random = new Random();
                    int skip = 0;
                    if (totalMatchingDocs > qty)
                    {
                        if (qty == 5)
                        {
                            skip = random.Next(0, (int)totalMatchingDocs - qty.Value);
                        }
                        else
                        {
                            skip = ((pageNumber ?? 1) - 1) * (qty ?? 50);
                        }
                    }

                    var meals = await _mealsCollection.Find(filter)
                                                      .Skip(skip)
                                                      .Limit(qty.Value)
                                                      .ToListAsync();

                    var userIds = meals.Select(meal => meal.UserId).Distinct().ToList();

                    var users = await _context.AppUser
                                        .Where(user => userIds.Contains(user.Id))
                                        .ToDictionaryAsync(user => user.Id, user => new { user.Name, user.Pfp });

                    res = meals.Select(meal => new SimpleMealVMO
                    {
                        Id = meal.Id,
                        StringId = meal.Id.ToString(),
                        Name = WebUtility.HtmlDecode(meal.Name),
                        Time = meal.Time,
                        Img = meal.Img,
                        Desc = meal.Description,
                        Ingredients = meal.Ingridients,
                        Steps = meal.Steps,
                        Kcal = meal.MealsMakro.Kcal,
                        Protein = meal.MealsMakro.Protein,
                        Fats = meal.MealsMakro.Fats,
                        Carbs = meal.MealsMakro.Carbs,
                        Servings = meal.MealsMakro.Servings??0,
                        SavedCounter = meal.SavedCounter,
                        Difficulty = meal.Difficulty ?? "Easy",
                        LikedCounter = meal.LikedCounter,
                        CreatorName = users.ContainsKey(meal.UserId) ? users[meal.UserId].Name : "Unknown",
                        CreatorId = meal.UserId,
                        CreatorPfp = users.ContainsKey(meal.UserId) ? users[meal.UserId].Pfp : "/pfp-images/e2f56642-a493-4c6d-924b-d3072714646a.png",
                        Liked = CheckIfLiked(LikedMeals, meal.Id.ToString()),
                        Saved = CheckIfLiked(SavedMeals, meal.Id.ToString()),
                        Own = meal.UserId == userId
                    }).ToList();
                }

                return (res, ErrorResponse.Ok());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get data by main category. Method: {nameof(GetByMainCategory)}");
                return (res, ErrorResponse.Internal(ex.Message));
            }
        }

        public async Task<(List<SimpleMealVMO> res, ErrorResponse error)> GetByHighMakro(string userId, List<string> LikedMeals, List<string> SavedMeals, string makroComponent, int? qty = 5, int? pageNumber = 1)
        {
            List<SimpleMealVMO> res = new List<SimpleMealVMO>();

            try
            {
                FilterDefinition<MealsDocument> filter;
                switch (makroComponent.ToLower())
                {
                    case "protein":
                        filter = Builders<MealsDocument>.Filter.Gt(meal => meal.MealsMakro.Protein, 20);
                        break;
                    case "carbs":
                        filter = Builders<MealsDocument>.Filter.Gt(meal => meal.MealsMakro.Carbs, 50);
                        break;
                    case "fats":
                        filter = Builders<MealsDocument>.Filter.Gt(meal => meal.MealsMakro.Fats, 15);
                        break;
                    default:
                        return (res, ErrorResponse.StateNotValid<string>("Invalid macro component."));
                }

                var totalMatchingDocs = await _mealsCollection.CountDocumentsAsync(filter);

                var random = new Random();
                int skip = 0;
                if (totalMatchingDocs > qty)
                {
                    if (qty == 5)
                    {
                        skip = random.Next(0, (int)totalMatchingDocs - qty.Value);
                    }
                    else
                    {
                        skip = ((pageNumber ?? 1) - 1) * (qty ?? 50);
                    }
                }

                var meals = await _mealsCollection.Find(filter)
                                                  .Skip(skip)
                                                  .Limit(qty.Value)
                                                  .ToListAsync();

                var userIds = meals.Select(meal => meal.UserId).Distinct().ToList();

                using (var _context = _contextFactory.CreateDbContext())
                {
                    var users = await _context.AppUser
                                        .Where(user => userIds.Contains(user.Id))
                                        .ToDictionaryAsync(user => user.Id, user => new { user.Name, user.Pfp });

                    res = meals.Select(meal => new SimpleMealVMO
                    {
                        Id = meal.Id,
                        StringId = meal.Id.ToString(),
                        Name = WebUtility.HtmlDecode(meal.Name),
                        Time = meal.Time,
                        Desc = meal.Description,
                        Ingredients = meal.Ingridients,
                        Steps = meal.Steps,
                        Difficulty = meal.Difficulty ?? "Easy",
                        Protein = meal.MealsMakro.Protein,
                        Fats = meal.MealsMakro.Fats,
                        Carbs = meal.MealsMakro.Carbs,
                        Servings = meal.MealsMakro.Servings ?? 0,
                        Img = meal.Img,
                        Kcal = meal.MealsMakro.Kcal,
                        SavedCounter = meal.SavedCounter,
                        LikedCounter = meal.LikedCounter,
                        CreatorName = users.ContainsKey(meal.UserId) ? users[meal.UserId].Name : "Unknown",
                        CreatorId = meal.UserId,
                        CreatorPfp = users.ContainsKey(meal.UserId) ? users[meal.UserId].Pfp : "/pfp-images/e2f56642-a493-4c6d-924b-d3072714646a.png",
                        Liked = CheckIfLiked(LikedMeals, meal.Id.ToString()), 
                        Saved = CheckIfLiked(SavedMeals, meal.Id.ToString()),
                        Own = meal.UserId == userId
                    }).ToList();
                }

                return (res, ErrorResponse.Ok());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get meal data by makro. Method: {nameof(GetByHighMakro)}");
                return (res, ErrorResponse.Internal(ex.Message));
            }
        }

        public async Task<(List<SimpleMealVMO> res, ErrorResponse error)> GetByLowMakro(string userId, List<string> LikedMeals, List<string> SavedMeals, string makroComponent, int? qty = 5, int? pageNumber = 1)
        {
            List<SimpleMealVMO> res = new List<SimpleMealVMO>();

            try
            {
                FilterDefinition<MealsDocument> filter;
                switch (makroComponent.ToLower())
                {
                    case "protein":
                        filter = Builders<MealsDocument>.Filter.Lt(meal => meal.MealsMakro.Protein, 10);
                        break;
                    case "carbs":
                        filter = Builders<MealsDocument>.Filter.Lt(meal => meal.MealsMakro.Carbs, 20);
                        break;
                    case "fats":
                        filter = Builders<MealsDocument>.Filter.Lt(meal => meal.MealsMakro.Fats, 5);
                        break;
                    default:
                        return (res, ErrorResponse.StateNotValid<string>("Invalid macro component."));
                }

                var totalMatchingDocs = await _mealsCollection.CountDocumentsAsync(filter);

                var random = new Random();
                int skip = 0;
                if (totalMatchingDocs > qty)
                {
                    if (qty == 5)
                    {
                        skip = random.Next(0, (int)totalMatchingDocs - qty.Value);
                    }
                    else
                    {
                        skip = ((pageNumber??1) - 1) * (qty ?? 50);
                    }
                }

                var meals = await _mealsCollection.Find(filter)
                                                  .Skip(skip)
                                                  .Limit(qty.Value)
                                                  .ToListAsync();

                var userIds = meals.Select(meal => meal.UserId).Distinct().ToList();

                using (var _context = _contextFactory.CreateDbContext())
                {
                    var users = await _context.AppUser
                                        .Where(user => userIds.Contains(user.Id))
                                        .ToDictionaryAsync(user => user.Id, user => new { user.Name, user.Pfp });

                    res = meals.Select(meal => new SimpleMealVMO
                    {
                        Id = meal.Id,
                        StringId = meal.Id.ToString(),
                        Name = WebUtility.HtmlDecode(meal.Name),
                        Time = meal.Time,
                        Img = meal.Img,
                        Desc = meal.Description,
                        Ingredients = meal.Ingridients,
                        Steps = meal.Steps,
                        Difficulty = meal.Difficulty ?? "Easy",
                        Protein = meal.MealsMakro.Protein,
                        Fats = meal.MealsMakro.Fats,
                        Carbs = meal.MealsMakro.Carbs,
                        Servings = meal.MealsMakro.Servings ?? 0,
                        Kcal = meal.MealsMakro.Kcal,
                        SavedCounter = meal.SavedCounter,
                        LikedCounter = meal.LikedCounter,
                        CreatorName = users.ContainsKey(meal.UserId) ? users[meal.UserId].Name : "Unknown",
                        CreatorId = meal.UserId,
                        CreatorPfp = users.ContainsKey(meal.UserId) ? users[meal.UserId].Pfp : "/pfp-images/e2f56642-a493-4c6d-924b-d3072714646a.png",
                        Liked = CheckIfLiked(LikedMeals, meal.Id.ToString()), 
                        Saved = CheckIfLiked(SavedMeals, meal.Id.ToString()),
                        Own = meal.UserId == userId
                    }).ToList();
                }

                return (res, ErrorResponse.Ok());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get meal data by makro. Method: {nameof(GetByLowMakro)}");
                return (res, ErrorResponse.Internal(ex.Message));
            }
        }

        public async Task<(List<SimpleMealVMO> res, ErrorResponse error)> GetMostLiked(string userId, List<string> LikedMeals, List<string> SavedMeals, int? qty = 5, int? pageNumber = 1)
        {
            List<SimpleMealVMO> res = new List<SimpleMealVMO>();

            try
            {
                int pageSize = qty ?? 5;
                int page = pageNumber ?? 1;
                int skipCount = (page - 1) * pageSize;

                var meals = await _mealsCollection.Find(Builders<MealsDocument>.Filter.Empty)
                                                  .Sort(Builders<MealsDocument>.Sort.Descending(meal => meal.LikedCounter))
                                                  .Limit(qty.Value)
                                                  .Skip(skipCount)
                                                  .ToListAsync();

                var userIds = meals.Select(meal => meal.UserId).Distinct().ToList();

                using (var _context = _contextFactory.CreateDbContext())
                {
                    var users = await _context.AppUser
                                        .Where(user => userIds.Contains(user.Id))
                                        .ToDictionaryAsync(user => user.Id, user => new { user.Name, user.Pfp });

                    res = meals.Select(meal => new SimpleMealVMO
                    {
                        Id = meal.Id,
                        StringId = meal.Id.ToString(),
                        Name = WebUtility.HtmlDecode(meal.Name),
                        Time = meal.Time,
                        Img = meal.Img,
                        Kcal = meal.MealsMakro.Kcal,
                        Desc = meal.Description,
                        Ingredients = meal.Ingridients,
                        Steps = meal.Steps,
                        Difficulty = meal.Difficulty ?? "Easy",
                        Protein = meal.MealsMakro.Protein,
                        Fats = meal.MealsMakro.Fats,
                        Carbs = meal.MealsMakro.Carbs,
                        Servings = meal.MealsMakro.Servings ?? 0,
                        SavedCounter = meal.SavedCounter,
                        LikedCounter = meal.LikedCounter,
                        CreatorName = users.ContainsKey(meal.UserId) ? users[meal.UserId].Name : "Unknown",
                        CreatorId = meal.UserId,
                        CreatorPfp = users.ContainsKey(meal.UserId) ? users[meal.UserId].Pfp : "/pfp-images/e2f56642-a493-4c6d-924b-d3072714646a.png",
                        Liked = CheckIfLiked(LikedMeals, meal.Id.ToString()),
                        Saved = CheckIfLiked(SavedMeals, meal.Id.ToString()),
                        Own = meal.UserId == userId
                    }).ToList();
                }

                return (res, ErrorResponse.Ok());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get data by liked nm. Method: {nameof(GetMostLiked)}");
                return (res, ErrorResponse.Internal(ex.Message));
            }
        }

        public async Task<(List<SimpleMealVMO> res, ErrorResponse error)> GetRandom(string userId, List<string> LikedMeals,List<string> SavedMeals,int? qty = 5, int? pageNumber = 1)
        {
            List<SimpleMealVMO> res = new List<SimpleMealVMO>();

            try
            {
                int skip = ((pageNumber??1) - 1) * (qty??50);

                var meals = await _mealsCollection.Aggregate()
                                                  .Sample(qty.Value)
                                                  .Skip(skip)
                                                  .ToListAsync();

                var userIds = meals.Select(meal => meal.UserId).Distinct().ToList();

                using (var _context = _contextFactory.CreateDbContext())
                {
                    var users = await _context.AppUser
                                        .Where(user => userIds.Contains(user.Id))
                                        .ToDictionaryAsync(user => user.Id, user => new { user.Name, user.Pfp });

                    res = meals.Select(meal => new SimpleMealVMO
                    {
                        Id = meal.Id,
                        StringId = meal.Id.ToString(),
                        Name = WebUtility.HtmlDecode(meal.Name),
                        Time = meal.Time,
                        Img = meal.Img,
                        Difficulty = meal.Difficulty??"Easy",
                        Kcal = meal.MealsMakro.Kcal,
                        Desc = meal.Description,
                        Ingredients = meal.Ingridients,
                        Steps = meal.Steps,
                        Protein = meal.MealsMakro.Protein,
                        Fats = meal.MealsMakro.Fats,
                        Carbs = meal.MealsMakro.Carbs,
                        Servings = meal.MealsMakro.Servings ?? 0,
                        SavedCounter = meal.SavedCounter,
                        LikedCounter = meal.LikedCounter,
                        CreatorName = users.ContainsKey(meal.UserId) ? users[meal.UserId].Name : "Unknown",
                        CreatorId = meal.UserId,
                        CreatorPfp = users.ContainsKey(meal.UserId) ? users[meal.UserId].Pfp : "/pfp-images/e2f56642-a493-4c6d-924b-d3072714646a.png",
                        Liked = CheckIfLiked(LikedMeals, meal.Id.ToString()),
                        Saved = CheckIfLiked(SavedMeals, meal.Id.ToString()),
                        Own = meal.UserId == userId
                    }).ToList();
                }

                return (res, ErrorResponse.Ok());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get random data. Method: {nameof(GetRandom)}");
                return (res, ErrorResponse.Internal(ex.Message));
            }
        }

        public async Task<(MealLikesDocument res, ErrorResponse error)> GetUserMealLikeDoc(string userId)
        {
            MealLikesDocument res = new MealLikesDocument();

            try
            {
                var filter = Builders<MealLikesDocument>.Filter.Eq(doc => doc.UserId, userId);
                var doc = await _mealLikesCollection.Find(filter).FirstOrDefaultAsync();

                if (doc != null)
                {
                    res = doc;
                    return (res, ErrorResponse.Ok());
                }

                _logger.LogWarning($"User liked-meals document not found. UserId: {userId} Method: {nameof(GetUserMealLikeDoc)}");
                return (res, ErrorResponse.NotFound("User Likes doc not found"));
            }
            catch (Exception ex) 
            {
                _logger.LogError($"Failed while trying to get user liked-meals doc. UserId: {userId} Method: {nameof(GetUserMealLikeDoc)}");
                return (res, ErrorResponse.Internal(ex.Message));
            }
        }

        public async Task<ErrorResponse> LikeMeal(string userId, string mealId)
        {
            try
            {
                var filter = Builders<MealLikesDocument>.Filter.Eq(x => x.UserId, userId);
                var doc = await _mealLikesCollection.Find(filter).FirstOrDefaultAsync();

                ObjectId objectId = new ObjectId(mealId);
                var mealFilter = Builders<MealsDocument>.Filter.Eq(x => x.Id, objectId);
                var mealDoc = await _mealsCollection.Find(mealFilter).FirstOrDefaultAsync();

                if (mealDoc == null)
                {
                    _logger.LogWarning($"meal not found. UserId: {userId} Method: {nameof(LikeMeal)}");
                    return ErrorResponse.NotFound("Meal not found");
                }

                int likeCounter = mealDoc.LikedCounter;

                if (doc == null)
                {
                    var newMealLikesDocument = new MealLikesDocument
                    {
                        UserId = userId,
                        SavedMeals = new List<string>(),
                        LikedMeals = new List<string> { mealId }
                    };

                    await _mealLikesCollection.InsertOneAsync(newMealLikesDocument);

                    likeCounter += 1;
                    var xmealUpdate = Builders<MealsDocument>.Update.Set(x => x.LikedCounter, likeCounter);
                    await _mealsCollection.UpdateOneAsync(mealFilter, xmealUpdate);

                    return ErrorResponse.Ok();
                }

                if (doc.LikedMeals.Contains(mealId))
                {
                    var update = Builders<MealLikesDocument>.Update.Pull(x => x.LikedMeals, mealId);
                    await _mealLikesCollection.UpdateOneAsync(filter, update);
                    likeCounter -= 1;
                }
                else
                {
                    var update = Builders<MealLikesDocument>.Update.Push(x => x.LikedMeals, mealId);
                    await _mealLikesCollection.UpdateOneAsync(filter, update);
                    likeCounter += 1;
                }

                var mealUpdate = Builders<MealsDocument>.Update.Set(x => x.LikedCounter, likeCounter);
                await _mealsCollection.UpdateOneAsync(mealFilter, mealUpdate);

                return ErrorResponse.Ok();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to like meal. UserId: {userId} MealId: {mealId} Method: {nameof(LikeMeal)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<ErrorResponse> SaveMeal(string userId, string mealId)
        {
            try
            {
                var filter = Builders<MealLikesDocument>.Filter.Eq(x => x.UserId, userId);
                var doc = await _mealLikesCollection.Find(filter).FirstOrDefaultAsync();

                ObjectId objectId = new ObjectId(mealId);
                var mealFilter = Builders<MealsDocument>.Filter.Eq(x => x.Id, objectId);
                var mealDoc = await _mealsCollection.Find(mealFilter).FirstOrDefaultAsync();

                if (mealDoc == null)
                {
                    _logger.LogWarning($"Meal not found. UserId: {userId} MealId: {mealId} Method: {nameof(SaveMeal)}");

                    var newDoc = await _helperService.CreateMissingDoc(userId, _mealsCollection);
                    if(newDoc == null)
                    {
                        return ErrorResponse.NotFound("Meal not found");
                    }

                    mealDoc = newDoc;
                }

                int saveCounter = mealDoc.SavedCounter;

                if (doc == null)
                {
                    var newMealLikesDocument = new MealLikesDocument
                    {
                        UserId = userId,
                        SavedMeals = new List<string> { mealId },
                        LikedMeals = new List<string>()
                    };

                    await _mealLikesCollection.InsertOneAsync(newMealLikesDocument);

                    saveCounter += 1;
                    var xmealUpdate = Builders<MealsDocument>.Update.Set(x => x.SavedCounter, saveCounter);
                    await _mealsCollection.UpdateOneAsync(mealFilter, xmealUpdate);

                    return ErrorResponse.Ok();
                }

                if (doc.SavedMeals.Contains(mealId))
                {
                    var update = Builders<MealLikesDocument>.Update.Pull(x => x.SavedMeals, mealId);
                    await _mealLikesCollection.UpdateOneAsync(filter, update);
                    saveCounter -= 1;
                }
                else
                {
                    var update = Builders<MealLikesDocument>.Update.Push(x => x.SavedMeals, mealId);
                    await _mealLikesCollection.UpdateOneAsync(filter, update);
                    saveCounter += 1;
                }

                var mealUpdate = Builders<MealsDocument>.Update.Set(x => x.SavedCounter, saveCounter);
                await _mealsCollection.UpdateOneAsync(mealFilter, mealUpdate);

                return ErrorResponse.Ok();
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, $"Failed while trying to save meal. UserId: {userId} MealId: {mealId} Method: {nameof(SaveMeal)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<(ErrorResponse error, List<SimpleMealVMO> res)> Search(string userId, SearchMealVM model)
        {
            List<SimpleMealVMO> res = new List<SimpleMealVMO>();
            Dictionary<string, UserData> users = new Dictionary<string, UserData>();

            try
            {
                var filterBuilder = Builders<MealsDocument>.Filter;
                var filters = new List<FilterDefinition<MealsDocument>>();

                if (!string.IsNullOrEmpty(model.Phrase))
                {
                    var phraseFilter = filterBuilder.Or(
                        filterBuilder.Regex("Name", new BsonRegularExpression(model.Phrase, "i")),
                        filterBuilder.Regex("Ingredients", new BsonRegularExpression(model.Phrase, "i"))
                    );
                    filters.Add(phraseFilter);
                }

                if (model.Nutritions != null)
                {
                    filters.Add(filterBuilder.Gte("MealsMakro.Kcal", model.Nutritions.MinimalCalories));
                    filters.Add(filterBuilder.Lte("MealsMakro.Kcal", model.Nutritions.MaximalCalories));
                    filters.Add(filterBuilder.Gte("MealsMakro.Protein", model.Nutritions.MinimalProtein));
                    filters.Add(filterBuilder.Lte("MealsMakro.Protein", model.Nutritions.MaximalProtein));
                    filters.Add(filterBuilder.Gte("MealsMakro.Fats", model.Nutritions.MinimumFats));
                    filters.Add(filterBuilder.Lte("MealsMakro.Fats", model.Nutritions.MaximumFats));
                    filters.Add(filterBuilder.Gte("MealsMakro.Carbs", model.Nutritions.MinimumCarbs));
                    filters.Add(filterBuilder.Lte("MealsMakro.Carbs", model.Nutritions.MaximumCarbs));
                }

                if (model.SearchTimeRange != null)
                {
                    filters.Add(filterBuilder.Gte("TimeMinutes", model.SearchTimeRange.MinimalTime));
                    filters.Add(filterBuilder.Lte("TimeMinutes", model.SearchTimeRange.MaximumTime));
                }

                var combinedFilters = filters.Count > 0 ? filterBuilder.And(filters) : filterBuilder.Empty;

                int skip = (model.PageNumber.Value - 1) * model.Qty.Value;

                List<MealsDocument> meals;
                if (model.SortValue == 0 && string.IsNullOrEmpty(model.Phrase))
                {
                    meals = await _mealsCollection.Aggregate()
                        .Match(combinedFilters)
                        .AppendStage<MealsDocument>("{ $sample: { size: " + model.Qty.Value + " } }")
                        .ToListAsync();
                }
                else
                {
                    var sortDefinition = Builders<MealsDocument>.Sort.Ascending("Name");
                    switch (model.SortValue)
                    {
                        case 1: sortDefinition = Builders<MealsDocument>.Sort.Ascending("Name"); break;
                        case 2: sortDefinition = Builders<MealsDocument>.Sort.Descending("Name"); break;
                        case 3: sortDefinition = Builders<MealsDocument>.Sort.Descending("LikedCounter"); break;
                        case 4: sortDefinition = Builders<MealsDocument>.Sort.Ascending("MealsMakro.Kcal"); break;
                        case 5: sortDefinition = Builders<MealsDocument>.Sort.Descending("MealsMakro.Kcal"); break;
                        case 6: sortDefinition = Builders<MealsDocument>.Sort.Ascending("MealsMakro.Protein"); break;
                        case 7: sortDefinition = Builders<MealsDocument>.Sort.Descending("MealsMakro.Protein"); break;
                        case 8: sortDefinition = Builders<MealsDocument>.Sort.Ascending("MealsMakro.Fats"); break;
                        case 9: sortDefinition = Builders<MealsDocument>.Sort.Descending("MealsMakro.Fats"); break;
                        case 10: sortDefinition = Builders<MealsDocument>.Sort.Ascending("MealsMakro.Carbs"); break;
                        case 11: sortDefinition = Builders<MealsDocument>.Sort.Descending("MealsMakro.Carbs"); break;
                    }

                    meals = await _mealsCollection.Find(combinedFilters)
                        .Sort(sortDefinition)
                        .Skip(skip)
                        .Limit(model.Qty.Value)
                        .ToListAsync();
                }

                var userIds = meals.Select(meal => meal.UserId).Distinct().ToList();

                using (var _context = _contextFactory.CreateDbContext())
                {
                    users = await _context.AppUser
                                        .Where(user => userIds.Contains(user.Id))
                                        .ToDictionaryAsync(user => user.Id, user => new UserData { Name = user.Name, Pfp = user.Pfp });
                }

                var userLikesDoc = await _mealLikesCollection.Find(l => l.UserId == userId).FirstOrDefaultAsync();
                var likedMeals = userLikesDoc?.LikedMeals ?? new List<string>();
                var savedMeals = userLikesDoc?.SavedMeals ?? new List<string>();

                res = meals.Select(meal => new SimpleMealVMO
                {
                    Id = meal.Id,
                    StringId = meal.Id.ToString(),
                    Name = WebUtility.HtmlDecode(meal.Name),
                    Time = meal.Time,
                    Img = meal.Img,
                    Desc = meal.Description,
                    Ingredients = meal.Ingridients,
                    Steps = meal.Steps,
                    Difficulty = meal.Difficulty ?? "Easy",
                    Protein = meal.MealsMakro.Protein,
                    Fats = meal.MealsMakro.Fats,
                    Carbs = meal.MealsMakro.Carbs,
                    Servings = meal.MealsMakro.Servings ?? 0,
                    Kcal = meal.MealsMakro.Kcal,
                    SavedCounter = meal.SavedCounter,
                    LikedCounter = meal.LikedCounter,
                    CreatorName = users.ContainsKey(meal.UserId) ? users[meal.UserId].Name : "Unknown",
                    CreatorId = meal.UserId,
                    CreatorPfp = users.ContainsKey(meal.UserId) ? users[meal.UserId].Pfp : "/pfp-images/e2f56642-a493-4c6d-924b-d3072714646a.png",
                    Liked = likedMeals.Contains(meal.Id.ToString()),
                    Saved = savedMeals.Contains(meal.Id.ToString()),
                    Own = meal.UserId == userId
                }).ToList();

                return (ErrorResponse.Ok(), res);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Faile while trying to perform meal search. Data: {model} Method: {nameof(Search)}");
                return (ErrorResponse.Internal(ex.Message), res);
            }
        }



        public async Task<(ErrorResponse error, List<SimpleMealVMO> res)> GetUserLikedMeals(string userId)
        {
            List<SimpleMealVMO> res = new List<SimpleMealVMO>();

            try
            {
                var filter = Builders<MealLikesDocument>.Filter.Eq(x => x.UserId, userId);
                var doc = await _mealLikesCollection.Find(filter).FirstOrDefaultAsync();
                if (doc == null)
                {
                    _logger.LogWarning($"User liked meals doc not found. UserId: {userId} Method: {nameof(GetUserLikedMeals)}");

                    var newDoc = await _helperService.CreateMissingDoc(userId, _mealLikesCollection);
                    if(newDoc == null)
                    {
                        return (ErrorResponse.NotFound("User likes document not found."), res);
                    }

                    doc = newDoc;
                }

                var likedMealIds = doc.LikedMeals.Select(ObjectId.Parse).ToList();
                var mealsFilter = Builders<MealsDocument>.Filter.In(x => x.Id, likedMealIds);
                var likedMeals = await _mealsCollection.Find(mealsFilter).ToListAsync();

                if (likedMeals == null || !likedMeals.Any())
                {
                    return (ErrorResponse.Ok("No meals found for liked meals."), res);
                }

                var userIds = likedMeals.Select(meal => meal.UserId).Distinct().ToList();

                using (var _context = _contextFactory.CreateDbContext())
                {
                    var users = await _context.AppUser
                                              .Where(user => userIds.Contains(user.Id))
                                              .ToDictionaryAsync(user => user.Id, user => new { user.Name, user.Pfp });

                    res = likedMeals.Select(meal => new SimpleMealVMO
                    {
                        Id = meal.Id,
                        StringId = meal.Id.ToString(),
                        Name = WebUtility.HtmlDecode(meal.Name),
                        Time = meal.Time,
                        Img = meal.Img,
                        Desc = meal.Description,
                        Ingredients = meal.Ingridients,
                        Steps = meal.Steps,
                        Difficulty = meal.Difficulty ?? "Easy",
                        Protein = meal.MealsMakro.Protein,
                        Fats = meal.MealsMakro.Fats,
                        Carbs = meal.MealsMakro.Carbs,
                        Servings = meal.MealsMakro.Servings ?? 0,
                        Kcal = meal.MealsMakro.Kcal,
                        SavedCounter = meal.SavedCounter,
                        LikedCounter = meal.LikedCounter,
                        CreatorName = users.ContainsKey(meal.UserId) ? users[meal.UserId].Name : "Unknown",
                        CreatorId = meal.UserId,
                        CreatorPfp = users.ContainsKey(meal.UserId) ? users[meal.UserId].Pfp : "/pfp-images/e2f56642-a493-4c6d-924b-d3072714646a.png",
                        Liked = true, 
                        Saved = doc.SavedMeals.Contains(meal.Id.ToString()),
                        Own = meal.UserId == userId
                    }).ToList();
                }

                return (ErrorResponse.Ok(), res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get user liked meals. UserId: {userId} Method: {nameof(GetUserLikedMeals)}");
                return (ErrorResponse.Internal(ex.Message), res);
            }
        }


        public async Task<(ErrorResponse error, List<SimpleMealVMO> res)> GetUserSavedMeals(string userId)
        {
            List<SimpleMealVMO> res = new List<SimpleMealVMO>();

            try
            {
                var filter = Builders<MealLikesDocument>.Filter.Eq(x => x.UserId, userId);
                var doc = await _mealLikesCollection.Find(filter).FirstOrDefaultAsync();
                if (doc == null)
                {
                    _logger.LogWarning($"User like document not found. UserId: {userId} Method: {GetUserSavedMeals}");

                    var newDoc = await _helperService.CreateMissingDoc(userId, _mealLikesCollection);
                    if(newDoc == null)
                    {
                        return (ErrorResponse.Ok("User saved document is null."), res);
                    }

                    doc = newDoc;
                }

                var savedMealIds = doc.SavedMeals.Select(ObjectId.Parse).ToList();
                var mealsFilter = Builders<MealsDocument>.Filter.In(x => x.Id, savedMealIds);
                var savedMeals = await _mealsCollection.Find(mealsFilter).ToListAsync();

                if (savedMeals == null || !savedMeals.Any())
                {
                    return (ErrorResponse.Ok("No meals found for saved meals."), res);
                }

                var userIds = savedMeals.Select(meal => meal.UserId).Distinct().ToList();

                using (var _context = _contextFactory.CreateDbContext())
                {
                    var users = await _context.AppUser
                                              .Where(user => userIds.Contains(user.Id))
                                              .ToDictionaryAsync(user => user.Id, user => new { user.Name, user.Pfp });

                    res = savedMeals.Select(meal => new SimpleMealVMO
                    {
                        Id = meal.Id,
                        StringId = meal.Id.ToString(),
                        Name = WebUtility.HtmlDecode(meal.Name),
                        Time = meal.Time,
                        Img = meal.Img,
                        Desc = meal.Description,
                        Ingredients = meal.Ingridients,
                        Steps = meal.Steps,
                        Difficulty = meal.Difficulty ?? "Easy",
                        Protein = meal.MealsMakro.Protein,
                        Fats = meal.MealsMakro.Fats,
                        Carbs = meal.MealsMakro.Carbs,
                        Servings = meal.MealsMakro.Servings ?? 0,
                        Kcal = meal.MealsMakro.Kcal,
                        SavedCounter = meal.SavedCounter,
                        LikedCounter = meal.LikedCounter,
                        CreatorName = users.ContainsKey(meal.UserId) ? users[meal.UserId].Name : "Unknown",
                        CreatorId = meal.UserId,
                        CreatorPfp = users.ContainsKey(meal.UserId) ? users[meal.UserId].Pfp : "/pfp-images/e2f56642-a493-4c6d-924b-d3072714646a.png",
                        Liked = doc.LikedMeals.Contains(meal.Id.ToString()), 
                        Saved = true ,
                        Own = meal.UserId == userId
                    }).ToList();
                }

                return (ErrorResponse.Ok(), res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get user saved meals. UserId: {userId} Method: {GetUserSavedMeals}");
                return (ErrorResponse.Internal(ex.Message), res);
            }
        }

        public async Task<ErrorResponse> PublishMeal(string userId, PublishMealVM model, IClientSessionHandle mongoSession)
        {
            try
            {
                string imageLink = $"/meal-images/deafult-meal-img.jpg"; //def image
                List<string> categories = model.Tags?.Where(tag => new[] { "Side Dish", "Main Dish", "Breakfast" }.Contains(tag)).ToList() ?? new List<string>();
                
                if (model.Image != null)
                {
                    Random rnd = new Random();
                    string extension = Path.GetExtension(model.Image.FileName);
                    extension = string.IsNullOrEmpty(extension) ? "jpg" : extension.TrimStart('.');
                    string newImageName = $"{Guid.NewGuid()}{rnd.Next(1, 1000000)}.{extension}";

                    string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets/Images/MealImages");

                    Directory.CreateDirectory(folderPath);

                    string fullPath = Path.Combine(folderPath, newImageName);

                    using (var fileStream = new FileStream(fullPath, FileMode.Create))
                    {
                        await model.Image.CopyToAsync(fileStream);
                    }

                    imageLink = $"/meal-images/{newImageName}";
                }

                //TODO
                CategoryExtraction();

                MealsDocument doc = new MealsDocument()
                {
                    Name = model.Name,
                    UserId = userId,
                    Description = model.Desc??"",
                    TimeMinutes = ConvertUserTimeToInt(model.Time??"30"),
                    Time = string.IsNullOrWhiteSpace(model.Time) ? "30 minutes" : model.Time + " minutes",
                    Steps = model.Steps,
                    Ingridients = model.Ingridients,
                    Img = imageLink,
                    Tags = model.Tags ?? new List<string>(),
                    Categories = categories,
                    MealsMakro = model.Makro,
                    Difficulty = SetMealDifficulty(ConvertUserTimeToInt(model.Time ?? "30"), model.Ingridients.Count(), model.Steps.Count()),
                };
                await _mealsCollection.InsertOneAsync(mongoSession ,doc);
                var createdMealId = doc.Id;

                var userRecord = await _ownMealCollection.Find(r => r.UserId == userId).FirstOrDefaultAsync();
                if (userRecord == null)
                {
                    OwnMealsDocument ownMealsDocument = new OwnMealsDocument()
                    {
                        UserId = userId,
                        OwnMealsId = new List<string>{ createdMealId.ToString() },
                        SavedIngMeals = new List<MealPlan>()
                    };
                    await _ownMealCollection.InsertOneAsync(mongoSession ,ownMealsDocument);
                }
                else
                {
                    if (!userRecord.OwnMealsId.Contains(createdMealId.ToString()))
                    {
                        userRecord.OwnMealsId.Add(createdMealId.ToString());
                        var update = Builders<OwnMealsDocument>.Update.Set(r => r.OwnMealsId, userRecord.OwnMealsId);
                        await _ownMealCollection.UpdateOneAsync(mongoSession, r => r.UserId == userId, update);
                    }
                }
             
                return ErrorResponse.Ok();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to publish meal. UserId: {userId} Data: {model} Method: {nameof(PublishMeal)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        private bool CheckIfLiked(List<string> liked, string mealId)
        {
            return liked != null && liked.Contains(mealId);
        }

        private int ConvertUserTimeToInt(string inputString)
        {
            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(inputString, @"^\d+");

                if (match.Success)
                {
                    return int.Parse(match.Value);
                }
                else
                {
                    return 30;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"couldn't parse. Method: {nameof(ConvertUserTimeToInt)}");
                throw new Exception("Couldn't handle parsing user time brackets.", ex);
            }
        }

        private string SetMealDifficulty(int time, int ingCounter, int stepCounter)
        {
            double counter = 0;
            string res = "Easy";

            if (time > 120) counter += 0.6;
            if (ingCounter > 8) counter += 0.1;
            if (stepCounter > 10) counter += 0.3;

            if (counter >= 1)
            {
                res = "Hard";
            }
            else if (counter >= 0.6)
            {
                res = "Medium";
            }

            return res;
        }

        private void CategoryExtraction()
        {

        }

        public async Task<(ErrorResponse error, List<SimpleMealVMO>? res)> GetOwnMeals(string userId, List<string> LikedMeals, List<string> SavedMeals)
        {
            try
            {
                var ownMealDosc = await _ownMealCollection.Find(r => r.UserId == userId).FirstOrDefaultAsync();
                if(ownMealDosc == null)
                {
                    return (ErrorResponse.Ok(), new List<SimpleMealVMO>());
                }

                var ownMeals = await _mealsCollection.Find(a => ownMealDosc.OwnMealsId.Contains(a.Id.ToString())).ToListAsync();
                var userIds = ownMeals.Select(meal => meal.UserId).Distinct().ToList();


                using (var _context = _contextFactory.CreateDbContext())
                {
                    var users = await _context.AppUser
                        .Where(user => userIds.Contains(user.Id))
                        .ToDictionaryAsync(user => user.Id ?? "unknown", user => new { user.Name, user.Pfp });


                    var res = ownMeals.Select(meal => new SimpleMealVMO
                    {
                        Id = meal.Id,
                        StringId = meal.Id.ToString(),
                        Name = WebUtility.HtmlDecode(meal.Name) ?? "Meal",
                        Time = meal.Time,
                        Img = meal.Img,
                        Kcal = meal.MealsMakro.Kcal,
                        Desc = meal.Description,
                        Ingredients = meal.Ingridients ?? new List<string>(),
                        Steps = meal.Steps ?? new List<string>(),
                        Difficulty = meal.Difficulty ?? "Easy",
                        Protein = meal.MealsMakro.Protein,
                        Fats = meal.MealsMakro.Fats,
                        Carbs = meal.MealsMakro.Carbs,
                        Servings = meal.MealsMakro.Servings ?? 0,
                        SavedCounter = meal.SavedCounter,
                        LikedCounter = meal.LikedCounter,
                        CreatorName = users.ContainsKey(meal.UserId) ? users[meal.UserId].Name : "Unknown",
                        CreatorId = meal.UserId,
                        CreatorPfp = users.ContainsKey(meal.UserId) ? users[meal.UserId].Pfp : "/pfp-images/e2f56642-a493-4c6d-924b-d3072714646a.png",
                        Liked = LikedMeals.Contains(meal.Id.ToString()),
                        Saved = SavedMeals.Contains(meal.Id.ToString()),
                        Own = meal.UserId == userId
                    }).ToList();

                    return (ErrorResponse.Ok(), res ?? new List<SimpleMealVMO>());
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to GetOwnMeals. UserId: {userId} Method: {nameof(GetOwnMeals)}");
                return (ErrorResponse.Internal(ex.Message), null);
            }
        }

        public async Task<ErrorResponse> DeleteMeal(string userId, ObjectId mealId)
        {
            try
            {
                var mealDoc = await _mealsCollection.Find(r => r.Id == mealId).FirstOrDefaultAsync();
                if (mealDoc == null)
                {
                    _logger.LogWarning($"Meal not found. UserId: {userId} MealId: {mealId} Method: {nameof(DeleteMeal)}");

                    var newDoc = await _helperService.CreateMissingDoc(userId, _mealsCollection);
                    if(newDoc == null)
                    {
                        return ErrorResponse.NotFound("Couldn't find meal with given id");
                    }

                    mealDoc = newDoc;
                }

                if (mealDoc.UserId != userId) 
                {
                    _logger.LogInformation($"User {userId} tried to delete someone else's meal. Method: {nameof(DeleteMeal)}");
                    return ErrorResponse.Forbidden("Permission denied. Given meal is not created by the user.");
                }

                var deleteResult = await _mealsCollection.DeleteOneAsync(r => r.Id == mealId);

                if (deleteResult.DeletedCount > 0)
                {
                    return ErrorResponse.Ok();
                }
                else
                {
                    _logger.LogError($"Mongo update failed while trying to delete meal. UserId: {userId} MealId: {mealId} Method: {nameof(DeleteMeal)}");
                    return ErrorResponse.Failed();
                }
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, $"Failed while trying to delete meal. UserId: {userId} MealId: {mealId} Method: {nameof(DeleteMeal)}");
                return ErrorResponse.Internal(ex.Message);
            }
        }

        public async Task<(List<SimpleMealVMO> data, ErrorResponse error)> GetUserRecipes(string userId, int count, int skip, List<string> LikedMeals, List<string> SavedMeals)
        {
            try
            {
                var vmo = new List<SimpleMealVMO>();

                var user = await _context.AppUser.Where(a=>a.Id == userId).ToDictionaryAsync(user => user.Id, user => new { user.Name, user.Pfp });
                if(user == null)
                {
                    _logger.LogWarning($"Tried to acess non-existing user. UserId: {userId} Method: {nameof(GetUserRecipes)}");
                    return (vmo, ErrorResponse.NotFound("User does not exists."));
                }

                var ownMealDosc = await _ownMealCollection.Find(r => r.UserId == userId).FirstOrDefaultAsync();
                if (ownMealDosc == null)
                {
                    return (vmo, ErrorResponse.Ok("Empty"));
                }

                var paginatedMeals = await _mealsCollection.Find(a => ownMealDosc.OwnMealsId.Contains(a.Id.ToString())).Skip(skip).Limit(count).ToListAsync();
                vmo = paginatedMeals.Select(meal => new SimpleMealVMO
                {
                    Id = meal.Id,
                    StringId = meal.Id.ToString(),
                    Name = WebUtility.HtmlDecode(meal.Name),
                    Time = meal.Time,
                    Img = meal.Img,
                    Kcal = meal.MealsMakro.Kcal,
                    Desc = meal.Description,
                    Ingredients = meal.Ingridients ?? new List<string>(),
                    Steps = meal.Steps ?? new List<string>(),
                    Difficulty = meal.Difficulty ?? "Easy",
                    Protein = meal.MealsMakro.Protein,
                    Fats = meal.MealsMakro.Fats,
                    Carbs = meal.MealsMakro.Carbs,
                    Servings = meal.MealsMakro.Servings ?? 0,
                    SavedCounter = meal.SavedCounter,
                    LikedCounter = meal.LikedCounter,
                    CreatorName = user.ContainsKey(meal.UserId) ? user[meal.UserId].Name : "Unknown",
                    CreatorId = meal.UserId,
                    CreatorPfp = user.ContainsKey(meal.UserId) ? user[meal.UserId].Pfp : "/pfp-images/e2f56642-a493-4c6d-924b-d3072714646a.png",
                    Liked = LikedMeals.Contains(meal.Id.ToString()),
                    Saved = SavedMeals.Contains(meal.Id.ToString()),
                    Own = meal.UserId == userId
                }).ToList();

                return (vmo, ErrorResponse.Ok());
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Failed while trying to get user recipes. UserId: {userId} Method: {nameof(GetUserRecipes)}");
                return (new List<SimpleMealVMO>(), ErrorResponse.Internal(ex.Message));
            }
        }

        public class UserData
        {
            public string Name { get; set; }
            public string Pfp { get; set; }
        }

    }
}
