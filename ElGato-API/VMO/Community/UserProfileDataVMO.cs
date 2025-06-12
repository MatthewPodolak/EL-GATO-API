using ElGato_API.Models.Feed;
using ElGato_API.ModelsMongo.Cardio;
using ElGato_API.ModelsMongo.Training;
using System.Text.Json.Serialization;

namespace ElGato_API.VMO.Community
{
    public class UserProfileDataVMO
    {
        public GeneralProfileData GeneralProfileData { get; set; } = new GeneralProfileData();
        public PrivateProfileInformation? PrivateProfileInformation { get; set; }
    }

    public class GeneralProfileData
    {
        public string Name { get; set; }
        public string Desc { get; set; }
        public string Pfp { get; set; }
        public int FollowedCounter { get; set; }
        public int FollowersCounter { get; set; }
        public bool IsFollowed { get; set; }
        public bool IsPrivate { get; set; }
        public bool IsRequested { get; set; }
        public bool IsOwn { get; set; } = false;
    }

    public class PrivateProfileInformation
    {
        public List<EarnedBadges> EarnedBadges { get; set; } = new List<EarnedBadges>();
        public List<RecentCardioActivity> RecentCardioActivities { get; set; } = new List<RecentCardioActivity>();
        public List<LiftData> RecentLiftActivities { get; set; }
        public List<BestLiftData> BestLifts { get; set; }
        public Statistics Statistics { get; set; } = new Statistics();
        public CardioTrainingStatistics CardioStatistics { get; set; } = new CardioTrainingStatistics();
    }

    public class EarnedBadges 
    { 
        public string Name { get; set; }
        public string Img { get; set; }
        public double Threshold { get; set; }
        public double CurrentTotalProgress { get; set; }
        public BadgeType BadgeType { get; set; }
        public ChallengeGoalType ChallengeGoalType { get; set; }
    }

    public class RecentCardioActivity
    {
        public DateTime Date { get; set; }
        public string Name { get; set; }
        public string? Desc { get; set; }
        public TimeSpan Duration { get; set; }
        public double DistanceMeters { get; set; }
        public double DistanceFeet => DistanceMeters * 3.28084;
        public double SpeedKmH { get; set; }
        public double SpeedMph => SpeedKmH * 0.621371;
        public int AvgHeartRate { get; set; }
        public string? Route { get; set; }
        public int CaloriesBurnt { get; set; }
        public int FeelingPercentage { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ActivityType ActivityType { get; set; } = ActivityType.Workout;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ExerciseFeeling ExerciseFeeling { get; set; } = ExerciseFeeling.Neutral;
    }

    public class LiftData
    {
        public string Name {  get; set; }
        public List<ExerciseSeries> Series { get; set; } = new List<ExerciseSeries>();
        public DateTime Date { get; set; }
    }

    public class BestLiftData
    {
        public string Name { get; set; }
        public double WeightKg { get; set; }
        public double WeightLbs => WeightKg * 2.20462;
        public int Repetitions {  get; set; }
    }

    public class Statistics
    {
        public PeriodStats Weekly {  get; set; } = new PeriodStats();
        public PeriodStats YearToDay { get; set; } = new PeriodStats();
        public PeriodStats AllTime { get; set; } = new PeriodStats();
    }

    public class PeriodStats
    {
        public int CaloriesBurnt { get; set; } = 0;
        public double WeightKg { get; set; }
        public double WeightLbs => WeightKg * 2.20462;
        public int Steps { get; set; } = 0;
        public int ActivitiesCount { get; set; } = 0;
        public TimeSpan TimeSpentOnActivites { get; set; }
        public double TotalDistanceKm { get; set; } = 0;
        public double TotalDistanceMiles => TotalDistanceKm * 0.621371;
    }

    public class CardioTrainingStatistics
    {
        public List<CardioTrainingStatisticsSpec> Activities { get; set; } = new List<CardioTrainingStatisticsSpec>();
    }

    public class CardioTrainingStatisticsSpec 
    {
        public ActivityType ActivityType { get; set; } = ActivityType.Workout;
        public int TotalActivities { get; set; } = 0;
        public double TotalDistanceKm { get; set; } = 0;
        public double TotalDistanceMiles => TotalDistanceKm * 0.621371;
        public double TotalCaloriesBurnt { get; set; } = 0;
        public TimeSpan TotalTimeSpent {  get; set; }
    }

    public enum BadgeType
    {
        Achievment,
        Challange
    }
}
