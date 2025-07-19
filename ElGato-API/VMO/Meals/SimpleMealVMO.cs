using MongoDB.Bson;

namespace ElGato_API.VMO.Meals
{
    public class SimpleMealVMO
    {
        public ObjectId Id { get; set; }
        public string StringId { get; set; }
        public string Name { get; set; }
        public string Desc { get; set; }
        public List<string> Ingredients { get; set; }
        public List<string> Steps { get; set; }
        public double Servings { get; set; } = 0;
        public string Time { get; set; }
        public string Img { get; set; }
        public string Difficulty { get; set; } = "Easy";
        public double Kcal { get; set; }
        public double Protein { get; set; }
        public double Fats { get; set; }
        public double Carbs { get; set; }
        public int SavedCounter { get; set; } = 0;
        public int LikedCounter { get; set; } = 0;
        public string CreatorName { get; set; }
        public string? CreatorPfp { get; set; }
        public string CreatorId { get; set; }
        public bool Liked { get; set; } = false;
        public bool Saved { get; set; } = false;
        public bool Own {  get; set; } = false;
    }
}
