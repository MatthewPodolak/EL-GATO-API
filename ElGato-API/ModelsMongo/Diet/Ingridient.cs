﻿namespace ElGato_API.ModelsMongo.Diet
{
    public class Ingridient
    {
        public string Name { get; set; }
        public string publicId { get; set; }
        public double WeightValue { get; set; }
        public double PrepedFor { get; set; }
        public double Proteins { get; set; }
        public bool Servings { get; set; } = false;
        public double Carbs { get; set; }
        public double Fats { get; set; }
        public double EnergyKcal { get; set; }
        public double EnergyKj
        {
            get
            {
                return Math.Round(EnergyKcal * 4.18, 2);
            }
        }
    }
}
