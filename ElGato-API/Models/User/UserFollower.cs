namespace ElGato_API.Models.User
{
    public class UserFollower
    {
        public string FollowerId { get; set; }
        public AppUser Follower { get; set; }

        public string FolloweeId { get; set; }
        public AppUser Followee { get; set; }
    }
}
