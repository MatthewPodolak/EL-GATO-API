namespace ElGato_API.Models.User
{
    public class UserFollowerRequest
    {
        public int Id { get; set; }
        public string RequesterId { get; set; }
        public AppUser Requester { get; set; }

        public string TargetId { get; set; }
        public AppUser Target { get; set; }

        public bool IsPending { get; set; } = true;
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    }
}
