using ElGato_API.ModelsMongo.Cardio;
using System.ComponentModel.DataAnnotations;

namespace ElGato_API.VM.Achievments
{
    public class BadgeIncDataVM
    {
        public ActivityType ActivityType { get; set; } = ActivityType.Workout;

        [Required(ErrorMessage = "Distance is required")]
        public double Distance { get; set; }
        public int CaloriesBurnt { get; set; } = 0;
        public double Elevation { get; set; } = 0;
    }
}
