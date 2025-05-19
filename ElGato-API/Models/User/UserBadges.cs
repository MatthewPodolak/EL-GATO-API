using ElGato_API.Models.Feed;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElGato_API.Models.User
{
    public class UserBadges
    {
        public int Id { get; set; }

        public string UserId { get; set; }
        public AppUser User { get; set; }

        public int ChallangeId { get; set; }
        public Challange Challange { get; set; }

        public DateTime CompletedTime { get; set; }
    }
}
