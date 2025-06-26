using ElGato_API.VM.UserData;
using ElGato_API.VMO.Achievments;
using ElGato_API.VMO.ErrorResponse;

namespace ElGato_API.Interfaces.Orchesters
{
    public interface IUserDataOrchester
    {
        Task<AchievmentResponse> AddStepsForUser(string userId, AddStepsVM model);
    }
}
