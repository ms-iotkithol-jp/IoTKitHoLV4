#r "System.Threading.Tasks"

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;

public static async void Run(CloudBlockBlob  myBlob, string name, CloudTable outputTable, TraceWriter log)
{
    log.Info($"C# Blob trigger function Processed Name:{myBlob.Name} in the {myBlob.Parent.Container.Name}");

    var names = myBlob.Name.Split(new char [] {'/'});
    log.Info($"DeviceId:{names[0]} - FileName:{names[1]}");
    var blobName = names[1];

    var client = new System.Net.Http.HttpClient();
    string endpoint = "https://westus.api.cognitive.microsoft.com/emotion/v1.0/recognize";
    string key = "[your emotion api key]";

    string deviceId;
    DateTime takenTime;
    GetBlobInfo(blobName, out deviceId, out takenTime);

    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);

    using (var stream = new MemoryStream())
    {
        await myBlob.DownloadToStreamAsync(stream);
        log.Info($"Blob Size:{stream.Length} bytes");
        var fileSize = stream.Length;
        stream.Seek(0, SeekOrigin.Begin);
        var buf = new Byte[fileSize];
        stream.Read(buf, 0, (int)fileSize);
        using (var content = new System.Net.Http.ByteArrayContent(buf))
        {
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            var response = await client.PostAsync(endpoint, content);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var resultEmotions =await response.Content.ReadAsStringAsync();
                log.Info($"Emotion Result - {resultEmotions}");
                dynamic res = Newtonsoft.Json.JsonConvert.DeserializeObject(resultEmotions);
                int index = 0;
                foreach(dynamic re in res)
                {
                    dynamic faceLeftToken = re.SelectToken("faceRectangle.left");
                    double faceLeft = faceLeftToken.Value;
                    dynamic faceTopToken = re.SelectToken("faceRectangle.top");
                    double faceTop = faceTopToken.Value;
                    dynamic faceWidthToken = re.SelectToken("faceRectangle.width");
                    double faceWidth = faceWidthToken.Value;
                    dynamic faceHeightToken = re.SelectToken("faceRectangle.height");
                    double faceHeight = faceHeightToken.Value;

                    dynamic angerToken = re.SelectToken("scores.anger");
                    double anger = angerToken.Value;
                    dynamic contemptToken = re.SelectToken("scores.contempt");
                    double contempt = contemptToken.Value;
                    dynamic disgustToken = re.SelectToken("scores.disgust");
                    double disgust = disgustToken.Value;
                    dynamic fearToken = re.SelectToken("scores.fear");
                    double fear = fearToken.Value; 
                    dynamic happinessToken = re.SelectToken("scores.happiness");
                    double happiness = happinessToken.Value;
                    dynamic neutralToken = re.SelectToken("scores.neutral");
                    double neutral = neutralToken.Value;
                    dynamic sadnessToken = re.SelectToken("scores.sadness");
                    double sadness = sadnessToken.Value;
                    dynamic surpriseToken = re.SelectToken("scores.surprise");
                    double surprise = surpriseToken.Value;

                    var emotionScores = new EmotionScores
                    {
                        PartitionKey = myBlob.Parent.Container.Name,
                        RowKey = deviceId + takenTime.Ticks.ToString() + index.ToString(),
                        DeviceId = deviceId,
                        BlobName = myBlob.Name,
                        TakenTime = takenTime,
                        FaceLeft = faceLeft,
                        FaceTop = faceTop,
                        FaceWidth = faceWidth,
                        FaceHeight = faceHeight,
                        Anger = anger,
                        Contempt = contempt,
                        Disgust = disgust,
                        Fear = fear,
                        Happiness = happiness,
                        Neutral = neutral,
                        Sadness = sadness,
                        Surprise = surprise
                    };

                    var insertOp = TableOperation.Insert(emotionScores);
                    outputTable.Execute(insertOp);
                    log.Info("Emotion Result inserted into Table");
                    index++;
                }
            }
            else
            {
                log.Info($"Emotion API Failed - {response.StatusCode}");
            }
        }
    }
}

public static void GetBlobInfo(string blobName, out string deviceId, out DateTime takenTime)
{
    deviceId = "device";
    var regx = new System.Text.RegularExpressions.Regex(
@"^(?<deviceId>[\w\-.]+)_(?<yyyy>[0-9]{4})(?<MM>[0-9]{2})(?<dd>[0-9]{2})_(?<hh>[0-9]{2})_(?<mm>[0-9]{2})_(?<ss>[0-9]{2})_Pro\.jpg$");
    var match = regx.Match(blobName);
    if (match.Length > 0)
    {
        deviceId = match.Groups["deviceId"].Value;
        var datetime = new string[7];
        int index = 0;
        datetime[index++] = match.Groups["yyyy"].Value;
        datetime[index++] = match.Groups["MM"].Value;
        datetime[index++] = match.Groups["dd"].Value;
        datetime[index++] = match.Groups["hh"].Value;
        datetime[index++] = match.Groups["mm"].Value;
        datetime[index++] = match.Groups["ss"].Value;
        datetime[index++] = "000";

        var datetimeInt = new int[datetime.Length];
        for (int i = 0; i < datetime.Length; i++)
        {
            if (i > 0)
            {
                var dt = datetime[i];
                if (datetime[i].StartsWith("0"))
                {
                    dt = datetime[i].Substring(1);
                }
                datetimeInt[i] = int.Parse(dt);
            }
            else
            {
                datetimeInt[i] = int.Parse(datetime[i]);
            }
        }

        takenTime = new DateTime(datetimeInt[0], datetimeInt[1], datetimeInt[2], datetimeInt[3], datetimeInt[4], datetimeInt[5], datetimeInt[6]);
    }
    else
    {
        takenTime = DateTime.Now;
    }
}

public class EmotionScores : TableEntity
{
    public string DeviceId { get; set; }

    public string BlobName { get; set; }
    public DateTime TakenTime { get; set; }

    public double FaceLeft { get; set; }
    public double FaceTop { get; set; }
    public double FaceWidth { get; set; }
    public double FaceHeight { get; set; }

    public double Anger { get; set; }
    public double Contempt { get; set; }
    public double Disgust { get; set; }
    public double Fear { get; set; }
    public double Happiness { get; set; }
    public double Neutral { get; set; }
    public double Sadness { get; set; }
    public double Surprise { get; set; }
}
