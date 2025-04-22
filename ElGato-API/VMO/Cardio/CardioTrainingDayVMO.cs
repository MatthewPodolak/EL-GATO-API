
using ElGato_API.ModelsMongo.Cardio;

namespace ElGato_API.VMO.Cardio
{
    public class CardioTrainingDayVMO
    {
        public DateTime Date { get; set; }
        public List<CardioTrainingDayExercviseVMO> Exercises { get; set; } = new List<CardioTrainingDayExercviseVMO>();
    }

    public class CardioTrainingDayExercviseVMO
    {
        public List<CardioTraining>? ExerciseData { get; set; }
        public PastCardioTrainingData? PastData { get; set; }
    }

    public class PastCardioTrainingData
    {
        public double SpeedKmh { get; set; }
        public double DistanceMeters { get; set; }
        public int AvgHeartRate { get; set; } = 0;
        public TimeSpan Duration { get; set; }
    }
}
