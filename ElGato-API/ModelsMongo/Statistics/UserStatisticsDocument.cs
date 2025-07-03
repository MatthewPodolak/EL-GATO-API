using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace ElGato_API.ModelsMongo.Statistics
{
    public class UserStatisticsDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string UserId { get; set; }
        public double TotalCaloriesCounter { get; set; }
        public double TotalWeightLiftedCounter { get; set; }
        public TimeSpan TotalTimeSpend { get; set; }
        public double TotalDistanceCounter {  get; set; }
        public int TotalStepsCounter { get; set; }
        public int TotalSessionsCounter { get; set; }
        public List<UserStatisticGroup> UserStatisticGroups { get; set; } = new List<UserStatisticGroup>();
    }

    public class UserStatisticGroup
    {
        public StatisticType Type { get; set; }
        public List<UserStatisticRecord> Records { get; set; } = new List<UserStatisticRecord>();
    }

    public class UserStatisticRecord 
    {
        public DateTime Date { get; set; }
        public double Value { get; set; } = 0;
        public TimeSpan TimeValue { get; set; } = TimeSpan.Zero;
    }

    public enum StatisticType
    {
        CaloriesBurnt,
        WeightLifted,
        StepsTaken,
        ActvSessionsCount,
        TimeSpend,
        TotalDistance,
        Weight
    }
}
