using ElGato_API.Data;
using ElGato_API.Data.JWT;
using ElGato_API.Interfaces;
using ElGato_API.Interfaces.Orchesters;
using ElGato_API.VM;
using ElGato_API.VM.User_Auth;
using ElGato_API.VMO.ErrorResponse;
using ElGato_API.VMO.UserAuth;
using MongoDB.Driver;

namespace ElGato_API.Services.Orchesters
{
    public class AccountOrchester : IAccountOrchester
    {
        private readonly ILogger<AccountOrchester> _logger;
        private readonly AppDbContext _context;
        private readonly IAccountService _accountService;
        private readonly IMongoClient _mongoClient;
        private readonly IMongoInits _mongoInits;
        private readonly IJwtService _jwtService;

        public AccountOrchester(ILogger<AccountOrchester> logger, AppDbContext context, IAccountService accountService, IMongoClient mongoClient, IMongoInits mongoInits, IJwtService jwtService)
        {
            _logger = logger;
            _context = context;
            _accountService = accountService;
            _mongoClient = mongoClient;
            _mongoInits = mongoInits;
            _jwtService = jwtService;
        }

        public async Task<RegisterVMO> RegisterWithQuestionary(RegisterWithQuestVM model)
        {
            var vmo = new RegisterVMO();

            await using var sqlTx = await _context.Database.BeginTransactionAsync();

            using var mongoSession = await _mongoClient.StartSessionAsync();
            mongoSession.StartTransaction();

            try
            {
                var regResult = await _accountService.RegisterUser(model);
                if (!regResult.Success)
                {
                    await sqlTx.RollbackAsync();
                    vmo.ErrorResponse = regResult;
                    return vmo;
                }

                var loginResult = await _accountService.LoginUser(new LoginVM
                {
                    Email = model.Email,
                    Password = model.Password
                });

                if (!loginResult.IdentityResult.Succeeded)
                {
                    throw new InvalidOperationException(string.Join("; ", loginResult.IdentityResult.Errors.Select(e => e.Description)));
                }

                vmo.ErrorResponse = ErrorResponse.Ok();
                vmo.JWT = loginResult.JwtToken;

                var userId = _jwtService.GetUserIdClaimStringBased(loginResult.JwtToken);

                await _mongoInits.CreateUserDietDocument(userId, mongoSession);
                await _mongoInits.CreateUserTrainingDocument(userId, mongoSession);
                await _mongoInits.CreateUserExerciseHistoryDocument(userId, mongoSession);

                await _context.SaveChangesAsync();
                await sqlTx.CommitAsync();

                await mongoSession.CommitTransactionAsync();

                return vmo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Orchestrated registration failed – rolling back both stores");
                await sqlTx.RollbackAsync();
                await mongoSession.AbortTransactionAsync();

                return new RegisterVMO { ErrorResponse = ErrorResponse.Internal(ex.Message) };
            }
        }
    }
}