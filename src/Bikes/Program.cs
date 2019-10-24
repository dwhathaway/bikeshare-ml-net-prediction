﻿using System;
using System.IO;
using BetterConsoleTables;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace Bikes
{
    class Program
    {
        /// <summary>
        /// The DemandObservation class holds one single bike demand observation record.
        /// </summary>
        public class DemandObservation
        {
            [LoadColumn(2)] public float Season { get; set; }
            [LoadColumn(3)] public float Year { get; set; }
            [LoadColumn(4)] public float Month { get; set; }
            [LoadColumn(5)] public float Hour { get; set; }
            [LoadColumn(6)] public float Holiday { get; set; }
            [LoadColumn(7)] public float Weekday { get; set; }
            [LoadColumn(8)] public float WorkingDay { get; set; }
            [LoadColumn(9)] public float Weather { get; set; }
            [LoadColumn(10)] public float Temperature { get; set; }
            [LoadColumn(11)] public float NormalizedTemperature { get; set; }
            [LoadColumn(12)] public float Humidity { get; set; }
            [LoadColumn(13)] public float Windspeed { get; set; }
            [LoadColumn(16)] [ColumnName("Label")] public float Count { get; set; }
        }

        /// <summary>
        /// The DemandPrediction class holds one single bike demand prediction.
        /// </summary>
        public class DemandPrediction
        {
            [ColumnName("Score")]
            public float PredictedCount;
        }

        private static string trainDataPath = Path.Combine(Environment.CurrentDirectory, "hour.csv");

        static void Main(string[] args)
        {
            // create the machine learning context
            var context = new MLContext();


            Console.WriteLine("Loading data...");

            var fullData = context.Data.LoadFromTextFile<DemandObservation>(
                path: trainDataPath,
                hasHeader: true,
                separatorChar: ',');

            // Split the data so that 20% of it is used to test the model afterwards
            var split = context.Data.TrainTestSplit(fullData, testFraction: 0.2);

            var trainingData = context.Data
                .CreateEnumerable<DemandObservation>(split.TrainSet, reuseRowObject: false);

            var testData = context.Data
                .CreateEnumerable<DemandObservation>(split.TestSet, reuseRowObject: false);

            // build a training pipeline
            // step 1: concatenate all feature columns
            var pipeline = context.Transforms.Concatenate(
                "Features",
                nameof(DemandObservation.Season),
                nameof(DemandObservation.Year),
                nameof(DemandObservation.Month),
                nameof(DemandObservation.Hour),
                nameof(DemandObservation.Holiday),
                nameof(DemandObservation.Weekday),
                nameof(DemandObservation.WorkingDay),
                nameof(DemandObservation.Weather),
                nameof(DemandObservation.Temperature),
                nameof(DemandObservation.NormalizedTemperature),
                nameof(DemandObservation.Humidity),
                nameof(DemandObservation.Windspeed))

                // step 2: cache the data to speed up training
                .AppendCacheCheckpoint(context);

            // set up an array of learners to try
            (string Name, IEstimator<ITransformer> Learner)[] regressionLearners =
            {
                ("SDCA", context.Regression.Trainers.Sdca()),
                ("Poisson", context.Regression.Trainers.LbfgsPoissonRegression()),
                ("FastTree", context.Regression.Trainers.FastTree()),
                ("FastTree Tweedie", context.Regression.Trainers.FastTreeTweedie())
            };

            // prepare a console table to hold the results
            var results = new Table(TableConfiguration.Unicode(), "Learner", "RMSE", "MSE", "MAE", "Prediction");

            // train the model on each learner
            foreach (var learner in regressionLearners)
            {
                Console.WriteLine($"Training and evaluating model using {learner.Name} learner...");

                // add the learner to the training pipeline
                var fullPipeline = pipeline.Append(learner.Learner);

                // train the model
                var trainedModel = fullPipeline.Fit(split.TrainSet);

                // evaluate the model
                var predictions = trainedModel.Transform(split.TestSet);
                var metrics = context.Regression.Evaluate(
                    data: predictions, 
                    labelColumnName: "Label",
                    scoreColumnName: "Score");

                // set up a sample observation
                var sample = new DemandObservation()
                {
                    Season = 3,
                    Year = 1,
                    Month = 8,
                    Hour = 10,
                    Holiday = 0,
                    Weekday = 4,
                    WorkingDay = 1,
                    Weather = 1,
                    Temperature = 0.8f,
                    NormalizedTemperature = 0.7576f,
                    Humidity = 0.55f,
                    Windspeed = 0.2239f
                };

                // create a prediction engine
                var engine = context.Model.CreatePredictionEngine<DemandObservation, DemandPrediction>(trainedModel);

                // make the prediction
                var prediction = engine.Predict(sample);

                // store all results in the console table
                results.AddRow(
                    learner.Name, 
                    metrics.RootMeanSquaredError.ToString("0.##"), 
                    metrics.MeanSquaredError.ToString("0.##"), 
                    metrics.MeanAbsoluteError.ToString("0.##"), 
                    prediction.PredictedCount.ToString("0"));

                // the rest of the code goes here...
            }

            // show the results
            Console.WriteLine(results.ToString());

            Console.ReadKey();
        }
    }
}
