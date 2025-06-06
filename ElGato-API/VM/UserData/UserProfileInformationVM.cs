namespace ElGato_API.VM.UserData
{
    public class UserProfileInformationVM
    {
        public string? NewName { get; set; }
        public string? NewDesc { get; set; }
        public IFormFile? NewImage { get; set; }
    }
}
