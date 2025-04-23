using System.ComponentModel.DataAnnotations;

namespace ElGato_API.VM.Cardio
{
    public class DeleteExercisesFromCardioTrainingVM
    {
        [Required(ErrorMessage = "Date is required")]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "Id is required")]
        [MinLength(1, ErrorMessage = "At least one exercise id is required to perform deletion")]
        public List<int> ExercisesIdToRemove { get; set; }
    }
}
