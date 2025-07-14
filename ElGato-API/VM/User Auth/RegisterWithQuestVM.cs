using ElGato_API.VMO.Questionary;
using System.ComponentModel.DataAnnotations;

namespace ElGato_API.VM.User_Auth
{
    public class RegisterWithQuestVM
    {

        [Required(ErrorMessage = "E-mail is necessary.")]
        [EmailAddress]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is necessary.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required(ErrorMessage = "Questionary is currently necessary.")]
        public QuestionaryVM Questionary { get; set; }
    }
}
