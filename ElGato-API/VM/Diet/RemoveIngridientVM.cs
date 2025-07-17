using System.ComponentModel.DataAnnotations;

namespace ElGato_API.VM.Diet
{
    public class RemoveIngridientVM
    {
        [Required(ErrorMessage = "Meal id is required")]
        public int MealPublicId { get; set; }

        [Required(ErrorMessage = "Ingredient id is required")]
        public string IngridientId { get; set; }

        [Required(ErrorMessage = "Ingredient name is required")]
        public string IngridientName { get; set; }

        [Required(ErrorMessage = "Ingredient weight value is required")]
        public double WeightValue { get; set; }

        [Required(ErrorMessage = "Date is required")]
        public DateTime Date { get; set; }
    }
}