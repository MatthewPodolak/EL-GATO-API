using ElGato_API.ModelsMongo.Cardio;
using MongoDB.Bson;

namespace ElGato_API.ModelsMongo.History
{
    public class CardioDailyHistoryDocument
    {
        public ObjectId Id { get; set; }
        public string UserId { get; set; }
        public List<CardioDailyHistoryTraining> Trainings { get; set; } = new List<CardioDailyHistoryTraining>();
    }

    public class CardioDailyHistoryTraining
    {
        public DateTime Date { get; set; }
        public CardioTraining CardioTraining { get; set; }
    }
}
