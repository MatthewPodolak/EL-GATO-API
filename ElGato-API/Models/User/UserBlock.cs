namespace ElGato_API.Models.User
{
    public class UserBlock
    {
        public string BlockerId { get; set; }
        public AppUser Blocker { get; set; }

        public string BlockedId { get; set; }
        public AppUser Blocked { get; set; }
    }
}
