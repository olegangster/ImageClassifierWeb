// ModelTools — інструмент для завдання із зірочкою.
//
//   eval    <model.mlnet> <папка_з_підпапками_класів> [поріг=0.70]
//           → показує зображення, де модель помиляється або має впевненість нижче порогу
//   retrain <папка_датасету_з_підпапками_класів> <вихідна_модель.mlnet>
//           → повторно навчає модель на розширеному датасеті
//   compare <до.mlnet> <після.mlnet> <папка_з_підпапками_класів> <звіт.md>
//           → порівнює дві моделі на одному наборі зображень
using System.Globalization;
using System.Text;
using ImageClassifierWeb;
using Microsoft.ML;
using Microsoft.ML.Data;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

try
{
    switch (args.FirstOrDefault()?.ToLowerInvariant())
    {
        case "eval" when args.Length >= 3:
            Eval(args[1], args[2], args.Length > 3 ? double.Parse(args[3], CultureInfo.InvariantCulture) : 0.70);
            break;
        case "retrain" when args.Length >= 3:
            Retrain(args[1], args[2]);
            break;
        case "compare" when args.Length >= 5:
            Compare(args[1], args[2], args[3], args[4]);
            break;
        default:
            Console.WriteLine("Використання:");
            Console.WriteLine("  ModelTools eval    <model.mlnet> <папка> [поріг]");
            Console.WriteLine("  ModelTools retrain <датасет> <out.mlnet>");
            Console.WriteLine("  ModelTools compare <до.mlnet> <після.mlnet> <папка> <звіт.md>");
            break;
    }
}
catch (Exception ex)
{
    Console.WriteLine("ПОМИЛКА: " + ex.Message);
    Environment.ExitCode = 1;
}

static List<Row> Evaluate(string modelPath, string folder)
{
    var ml = new MLContext(seed: 1);
    var model = ml.Model.Load(modelPath, out _);
    var engine = ml.Model.CreatePredictionEngine<MLModel1.ModelInput, MLModel1.ModelOutput>(model);

    var col = engine.OutputSchema.GetColumnOrNull("Label")
        ?? throw new Exception("У моделі немає колонки Label.");
    var keys = default(VBuffer<ReadOnlyMemory<char>>);
    col.GetKeyValues(ref keys);
    var labels = keys.DenseValues().Select(x => x.ToString()).ToArray();

    var rows = new List<Row>();
    foreach (var dir in Directory.GetDirectories(folder).OrderBy(d => d))
    {
        var trueLabel = Path.GetFileName(dir);
        foreach (var file in Directory.EnumerateFiles(dir)
                     .Where(f => Cfg.Exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                     .OrderBy(f => f))
        {
            var output = engine.Predict(new MLModel1.ModelInput { ImageSource = File.ReadAllBytes(file) });
            var best = Array.IndexOf(output.Score, output.Score.Max());
            rows.Add(new Row(Path.GetFileName(file), trueLabel, labels[best], output.Score[best]));
        }
    }

    if (rows.Count == 0)
        throw new Exception($"У папці '{folder}' не знайдено зображень у підпапках класів.");

    return rows;
}

static void Eval(string modelPath, string folder, double threshold)
{
    var rows = Evaluate(modelPath, folder);
    var hard = rows.Where(r => !r.Correct || r.Confidence < threshold).ToList();

    Console.WriteLine($"Усього зображень: {rows.Count}");
    Console.WriteLine($"Точність: {Pct(rows.Count(r => r.Correct), rows.Count)}");
    Console.WriteLine($"Середня впевненість: {rows.Average(r => r.Confidence):P1}");
    Console.WriteLine($"Проблемних (помилка або впевненість < {threshold:P0}): {hard.Count}");
    Console.WriteLine();

    foreach (var r in hard)
    {
        var why = r.Correct ? "низька впевненість" : "ПОМИЛКА";
        Console.WriteLine($"{r.File,-30} справжній={r.TrueLabel,-12} модель={r.Predicted,-12} {r.Confidence:P1}  [{why}]");
    }
}

static void Retrain(string datasetFolder, string outModel)
{
    var ml = new MLContext(seed: 1);
    Console.WriteLine("Завантаження датасету…");
    var data = MLModel1.LoadImageFromFolder(ml, datasetFolder);
    Console.WriteLine("Навчання (може тривати кілька хвилин)…");
    var model = MLModel1.RetrainModel(ml, data);
    ml.Model.Save(model, data.Schema, outModel);
    Console.WriteLine($"Збережено: {outModel}");
}

static void Compare(string before, string after, string folder, string reportPath)
{
    var b = Evaluate(before, folder);
    var a = Evaluate(after, folder);

    var sb = new StringBuilder();
    sb.AppendLine("# Порівняння моделі до і після донавчання");
    sb.AppendLine();
    sb.AppendLine("| Метрика | До | Після |");
    sb.AppendLine("|---|---|---|");
    sb.AppendLine($"| Зображень | {b.Count} | {a.Count} |");
    sb.AppendLine($"| Правильних | {b.Count(r => r.Correct)} | {a.Count(r => r.Correct)} |");
    sb.AppendLine($"| Точність | {Pct(b.Count(r => r.Correct), b.Count)} | {Pct(a.Count(r => r.Correct), a.Count)} |");
    sb.AppendLine($"| Середня впевненість | {b.Average(r => r.Confidence):P1} | {a.Average(r => r.Confidence):P1} |");
    sb.AppendLine($"| Впевненість < 70% | {b.Count(r => r.Confidence < 0.70)} | {a.Count(r => r.Confidence < 0.70)} |");
    sb.AppendLine();
    sb.AppendLine("## По кожному зображенню");
    sb.AppendLine();
    sb.AppendLine("| Файл | Справжній клас | До: клас | До: впевн. | Після: клас | Після: впевн. |");
    sb.AppendLine("|---|---|---|---|---|---|");
    for (int i = 0; i < b.Count; i++)
    {
        var x = b[i]; var y = a[i];
        var okB = x.Correct ? "✅" : "❌";
        var okA = y.Correct ? "✅" : "❌";
        sb.AppendLine($"| {x.File} | {x.TrueLabel} | {okB} {x.Predicted} | {x.Confidence:P1} | {okA} {y.Predicted} | {y.Confidence:P1} |");
    }

    File.WriteAllText(reportPath, sb.ToString(), new UTF8Encoding(true));
    Console.WriteLine(sb.ToString());
    Console.WriteLine($"Звіт збережено: {reportPath}");
}

static string Pct(int n, int total) => total == 0 ? "—" : ((double)n / total).ToString("P1");

record Row(string File, string TrueLabel, string Predicted, double Confidence)
{
    public bool Correct => string.Equals(TrueLabel, Predicted, StringComparison.OrdinalIgnoreCase);
}

static class Cfg
{
    public static readonly string[] Exts = { ".png", ".jpg", ".jpeg", ".gif" };
}
