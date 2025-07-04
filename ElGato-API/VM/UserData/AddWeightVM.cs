using System.ComponentModel.DataAnnotations;

namespace ElGato_API.VM.UserData
{
    public class AddWeightVM
    {
        public double? WeightMetric { get; set; }
        public double? WeightImperial { get; set; }

        [Required(ErrorMessage = "Date is required")]
        public DateTime Date { get; set; }
    }
}
