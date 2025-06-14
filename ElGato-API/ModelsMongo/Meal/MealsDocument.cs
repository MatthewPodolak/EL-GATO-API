using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ElGato_API.ModelsMongo.Meal
{
    public class MealsDocument
    {
        public ObjectId Id { get; set; }
        public string UserId { get; set; }
        public string Name { get; set; }
        public string Time { get; set; }
        public string Description { get; set; }
        public string Img { get; set; }
        public string? Link { get; set; }
        public double TimeMinutes { get; set; }
        [BsonElement("Difficulty")]
        public string? Difficulty { get; set; }
        public List<string> Ingridients { get; set; }
        public List<string>? IngridientsIds { get; set; }
        public List<string> Categories { get; set; }
        public List<string> Steps { get; set; }
        [BsonElement("tags")]
        public List<string>? Tags { get; set; }
        public MealsMakro MealsMakro { get; set; }
        public int SavedCounter { get; set; } = 0;
        public int LikedCounter { get; set; } = 0;
    }

    public class MealsMakro
    {
        public double Kcal { get; set; }
        public double Protein { get; set; }
        public double Carbs { get; set; }
        public double Fats { get; set; }
        public double? Servings { get; set; }
        public double PreperedPer { get; set; } = 0;
    }
}
