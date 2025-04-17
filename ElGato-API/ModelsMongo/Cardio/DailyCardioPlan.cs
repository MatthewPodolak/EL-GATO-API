namespace ElGato_API.ModelsMongo.Cardio
{
    public class DailyCardioPlan
    {
        public DateTime Date { get; set; }
        public List<CardioTraining> Exercises { get; set; } = new List<CardioTraining>();
    }
}
