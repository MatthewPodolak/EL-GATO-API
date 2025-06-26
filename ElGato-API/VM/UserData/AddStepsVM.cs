using System.ComponentModel.DataAnnotations;

namespace ElGato_API.VM.UserData
{
    public class AddStepsVM
    {
        public int Steps { get; set; } = 0;

        [Required (ErrorMessage = "Date is required")]
        public DateTime Date { get; set; }
    }
}
