namespace ElGato_API.VMO.User
{
    public class UserStepsHistoryVMO
    {
        public List<StepsRecords> Records { get; set; } = new List<StepsRecords>();
    }

    public class StepsRecords
    {
        public DateTime Date { get; set; }
        public int Value { get; set; }
    }
}
