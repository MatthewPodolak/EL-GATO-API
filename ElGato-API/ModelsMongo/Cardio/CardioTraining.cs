﻿using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;
using System.Text.Json.Serialization;

namespace ElGato_API.ModelsMongo.Cardio
{
    public class CardioTraining
    {
        public int PublicId { get; set; }
        public string Name { get; set; }
        public string? Desc { get; set; }
        public string? PrivateNotes { get; set; }
        public TimeSpan Duration { get; set; }
        public double DistanceMeters { get; set; }
        public double DistanceFeet => DistanceMeters * 3.28084;
        public double SpeedKmH { get; set; }
        public double SpeedMph => SpeedKmH * 0.621371;
        public int AvgHeartRate { get; set; }
        public string? Route { get; set; }
        public int CaloriesBurnt { get; set; }
        public int FeelingPercentage { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ActivityType ActivityType { get; set; } = ActivityType.Workout;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ExerciseFeeling ExerciseFeeling { get; set; } = ExerciseFeeling.Neutral;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ExerciseVisilibity ExerciseVisilibity { get; set; } = ExerciseVisilibity.Public;

    }

    public enum ExerciseFeeling
    {
        Terrible,
        Bad,
        Neutral,
        Good,
        Great
    }
    public enum ExerciseVisilibity
    {
        Private,
        Public
    }
    public enum ActivityType
    {
        Running,
        Walking,
        Hiking,
        Bike,
        MountainBike,
        EBike,
        Swimming,
        Surfing,
        Kayaking,
        Paddling,
        IceSkating,
        Snowboarding,
        Skiing,
        Football,
        Golf,
        Squash,
        Tennis,
        Badminton,
        Basketball,
        Volleyball,
        Workout
    }
}
