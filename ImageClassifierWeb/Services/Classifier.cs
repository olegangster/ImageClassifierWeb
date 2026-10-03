namespace ImageClassifierWeb.Services;

public static class Classifier
{
    private static readonly object Sync = new();

    public static List<KeyValuePair<string, float>> Classify(byte[] imageBytes)
    {
        var input = new MLModel1.ModelInput { ImageSource = imageBytes };

        // PredictionEngine не є потокобезпечним
        lock (Sync)
        {
            return MLModel1
                .PredictAllLabels(input)
                .OrderByDescending(x => x.Value)
                .ToList();
        }
    }
}
