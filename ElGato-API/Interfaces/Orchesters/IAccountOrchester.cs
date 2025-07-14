using Amazon.Runtime.Internal;
using ElGato_API.VM.User_Auth;
using ElGato_API.VMO.UserAuth;

namespace ElGato_API.Interfaces.Orchesters
{
    public interface IAccountOrchester
    {
        Task<RegisterVMO> RegisterWithQuestionary(RegisterWithQuestVM model);
    }
}
