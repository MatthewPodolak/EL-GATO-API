using ElGato_API.ModelsMongo.Cardio;
using System.ComponentModel.DataAnnotations;

namespace ElGato_API.VM.Cardio
{
    public class ChangeExerciseVisilibityVM
    {
        [Required(ErrorMessage = "Date is required")]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "Id is required")]
        public int ExerciseId { get; set; }

        [Required(ErrorMessage = "New state is required")]
        public ExerciseVisilibity State { get; set; }
    }
}
