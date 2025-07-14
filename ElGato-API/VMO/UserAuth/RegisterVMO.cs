using ElGato_API.VMO.Diet;
using ElGato_API.VMO.ErrorResponse;

namespace ElGato_API.VMO.UserAuth
{
    public class RegisterVMO
    {
        public string? JWT { get; set; }
        public ErrorResponse.ErrorResponse ErrorResponse { get; set; }
    }
}