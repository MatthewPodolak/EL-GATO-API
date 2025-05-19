namespace ElGato_API.Models.User
{
    public class Achievment
    {
        public int Id { get; set; }
        public string StringId { get; set; }
        public string Family {  get; set; }
        public string Name { get; set; }
        public string Desc { get; set; }
        public string GenerativeText { get; set; }
        public string Img { get; set; }
        public int Threshold { get; set; }
        public bool DailyLimit { get; set; } = false;
        public List<AppUser> Users { get; set; } = new List<AppUser>();
    }
}
