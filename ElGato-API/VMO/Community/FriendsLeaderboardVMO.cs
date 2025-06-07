namespace ElGato_API.VMO.Community
{
    public class FriendsLeaderboardVMO
    {
        public List<Leaderboard> Leaderboards { get; set; } = new List<Leaderboard>();
    }
    public class Leaderboard
    {
        public LeaderboardType Type { get; set; }
        public List<LeaderboardRecord> All { get; set; } = new List<LeaderboardRecord>();
        public List<LeaderboardRecord> Year { get; set; } = new List<LeaderboardRecord>();
        public List<LeaderboardRecord> Month { get; set; } = new List<LeaderboardRecord>();
        public List<LeaderboardRecord> Week { get; set; } = new List<LeaderboardRecord>();
    }

    public class LeaderboardRecord
    {
        public int LeaderboardPosition { get; set; }
        public LeaderboardUserData UserData { get; set; } = new LeaderboardUserData();
        public double Value { get; set; } = 0;
        public LeaderboardCardioData? CardioSpecific {  get; set; }
    }
    public class LeaderboardUserData
    {
        public string Name { get; set; }
        public string Pfp { get; set; }
    }

    public class LeaderboardCardioData
    {
        public double SpeedKmh { get; set; }
        public double SpeedMph => SpeedKmh * 0.621371;
        public double DistanceKm { get; set; }
        public double DistanceMiles => DistanceKm * 0.621371;
        public TimeSpan Time {  get; set; }
        public DateTime ExerciseDate { get; set; }
    }

    public enum LeaderboardType
    {
        Calories,
        Activity,
        Steps,
        Running,
        Swimming
    }
}
