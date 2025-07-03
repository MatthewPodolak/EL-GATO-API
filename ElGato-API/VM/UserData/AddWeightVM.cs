using System.ComponentModel.DataAnnotations;

namespace ElGato_API.VM.UserData
{
    public class AddWeightVM
    {
        [Required(ErrorMessage = "Weight is required")]
        public double Weight { get; set; }

        [Required(ErrorMessage = "Date is required")]
        public DateTime Date { get; set; }
    }
}
