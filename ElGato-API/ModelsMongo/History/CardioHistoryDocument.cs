﻿using ElGato_API.ModelsMongo.Cardio;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;

namespace ElGato_API.ModelsMongo.History
{
    public class CardioHistoryDocument
    {
        public ObjectId Id { get; set; }
        public string UserId { get; set; }
        public List<CardioHistoryExercise> Exercises { get; set; } = new List<CardioHistoryExercise>();
    }

    public class CardioHistoryExercise
    {
        public ActivityType ActivityType { get; set; } = ActivityType.Workout;
        public List<HistoryCardioExerciseData> ExercisesData { get; set; }
    }

    public class HistoryCardioExerciseData
    {
        public DateTime Date { get; set; }
        public TimeSpan Duration { get; set; }
        public double DistanceMeters { get; set; }
        public double SpeedKmH { get; set; }
        public int AvgHeartRate { get; set; }
        public int CaloriesBurnt {  get; set; }
    }
}
