using MongoDB.Bson;

namespace ElGato_API.ModelsMongo.Cardio
{
    public class DailyCardioDocument
    {
        public ObjectId Id { get; set; }
        public string UserId { get; set; }
        public List<DailyCardioPlan> Trainings { get; set; } = new List<DailyCardioPlan>();
    }
}
