using ElGato_API.ModelsMongo.Cardio;
using System.ComponentModel.DataAnnotations;

namespace ElGato_API.VM.Cardio
{
    public class AddCardioExerciseVM
    {
        [Required(ErrorMessage = "Date is required")]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "Name is required")]
        public string Name { get; set; }
        public string? Desc { get; set; }
        public string? PrivateNotes { get; set; }

        [Required(ErrorMessage = "Duration is required")]
        public TimeSpan Duration { get; set; }

        [Required(ErrorMessage = "Distance is required")]
        public double Distance { get; set; }

        [Required(ErrorMessage = "Speed is required")]
        public double Speed { get; set; }

        public int AvgHeartRate { get; set; } = 0;

        [Required(ErrorMessage = "Route is required")]
        public string EncodedRoute { get; set; }
        public bool IsMetric { get; set; } = true;

        [Required(ErrorMessage = "ActivityType is required")]
        public ActivityType ActivityType { get; set; }

        [Required(ErrorMessage = "ExerciseFeeling is required")]
        public ExerciseFeeling ExerciseFeeling { get; set; }

        [Required(ErrorMessage = "ExerciseVisilibity is required")]
        public ExerciseVisilibity ExerciseVisilibity { get; set; }
    }
}
