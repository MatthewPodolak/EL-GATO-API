using ElGato_API.VM;
using ElGato_API.VM.User_Auth;
using ElGato_API.VMO.ErrorResponse;
using ElGato_API.VMO.UserAuth;

namespace ElGato_API.Interfaces
{
    public interface IAccountService
    {
        Task<LoginVMO> LoginUser(LoginVM loginVM);
        Task<ErrorResponse> RegisterUser(RegisterWithQuestVM model);
        Task<bool> IsEmailAlreadyUsed(string email);
    }
}