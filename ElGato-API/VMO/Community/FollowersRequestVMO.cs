namespace ElGato_API.VMO.Community
{
    public class FollowersRequestVMO
    {
        public List<FollowerRequest> Requests { get; set; } = new List<FollowerRequest>();
    }
    public class FollowerRequest 
    { 
        public int RequestId { get; set; }
        public string UserId { get; set; }
        public string Name { get; set; }
        public string Pfp { get; set; }
        public DateTime DateRequested { get; set; }
    }

}
