using ElGato_API.ModelsMongo.Statistics;

namespace ElGato_API.VM.UserData
{
    public class UserStatisticsVM
    {
        public StatisticType Type { get; set; }
        public DateTime Date { get; set; }
        public double Value { get; set; } = 0;
        public TimeSpan? TimeValue { get; set; } = TimeSpan.Zero;
    }
}
