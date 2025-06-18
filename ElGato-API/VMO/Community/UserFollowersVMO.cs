namespace ElGato_API.VMO.Community
{
    public class UserFollowersVMO
    {
        public List<UserFollowersList> Followers { get; set; } = new List<UserFollowersList>();
        public List<UserFollowersList> Followed { get; set; } = new List<UserFollowersList>();
    }

    public class UserFollowersList
    {
        public string Name { get; set; }
        public string Pfp { get; set; }
        public string UserId { get; set; }
        public bool IsFollowed { get; set; }
        public bool FollowedByAskingUser { get; set; } = false;
        public bool IsPrivate { get; set; } = false;
        public bool IsRequested { get; set; } = false;
    }
}
