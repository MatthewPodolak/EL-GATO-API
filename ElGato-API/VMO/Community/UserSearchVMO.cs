namespace ElGato_API.VMO.Community
{
    public class UserSearchVMO
    {
        public List<UserSearch> Users { get; set; } = new List<UserSearch>();
    }

    public class UserSearch
    {
        public string UserId { get; set; }
        public string Name { get; set; }
        public string Pfp { get; set; }
    }
}
