﻿using System.ComponentModel.DataAnnotations;

namespace ElGato_API.VM.Diet
{
    public class AddMealFromSavedVM
    {
        [Required(ErrorMessage = "Name is required")]
        public string Name { get; set; }
        [Required(ErrorMessage = "Date is required")]
        public DateTime Date { get; set; }
    }
}
