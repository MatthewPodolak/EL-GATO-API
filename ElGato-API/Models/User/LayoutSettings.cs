namespace ElGato_API.Models.User
{
    public class LayoutSettings
    {
        public bool Animations { get; set; } = true;
        public List<ChartStack> ChartStack { get; set; }
    }

    public class ChartStack
    {
        public ChartType ChartType { get; set; }
        public ChartDataType ChartDataType { get; set; }
        public Period Period { get; set; }
        public string Name { get; set; }
    }

    public enum ChartType
    {
        Linear,
        Compare,
        Hexagonal,
        Bar,
        Circle
    }

    public enum ChartDataType
    {
        Exercise,
        Makro,
        Calorie,
        Protein,
        Fat,
        Water,
        Weight,
        NotDefined,
        Carbs,
        MakroDist,
        Steps
    }

    public enum Period
    {
        Week,
        Month,
        Year,
        All,
        Last,
        Last5,
        Last10,
        Last15
    }

}
