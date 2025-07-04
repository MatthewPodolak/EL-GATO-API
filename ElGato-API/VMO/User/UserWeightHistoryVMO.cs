namespace ElGato_API.VMO.User
{
    public class UserWeightHistoryVMO
    {
        public List<WeightRecord> Records { get; set; } = new List<WeightRecord>();
    }

    public class WeightRecord
    {
        public DateTime Date { get; set; }
        public double WeightMetric { get; set; }
        public double WeightImperial { get; set; }
    }
}
