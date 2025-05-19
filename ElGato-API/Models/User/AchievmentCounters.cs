namespace ElGato_API.Models.User
{
    public class AchievmentCounters
    {
        public int Id { get; set; }
        public DateTime LastCount { get; set; }
        public int Counter {  get; set; }
        public int AchievmentId { get; set; }
        public Achievment Achievment { get; set; }
        public string UserId { get; set; }
        public AppUser User { get; set; }
    }
}