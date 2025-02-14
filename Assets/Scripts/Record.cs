using System;
using System.Collections;
using System.Globalization;
using System.IO;

using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;

public enum VideoStage
{
    Reactions,
    Texts,
}


public class Record : MonoBehaviour
{
    public static Emotion emotion = Emotion.Max;
    public static bool action = false;
    public static VideoStage stage = VideoStage.Reactions;


    [SerializeField] private OVRFaceExpressions faceExpressions;
    [SerializeField] private RenderTexture renderTexture;
    // Reference to the Videos component to access VideoPlayer
    [SerializeField] private Videos videoComponent;

    private MovieRecorderSettings movieRecorderSettings;
    private bool recording = false;

    private string sessionFolder;

    private static readonly OVRFaceExpressions.FaceExpression[] expressionList = GetFaceExpressions();

    void Awake()
    {
        if (!faceExpressions)
        {
            Debug.LogError("OVRFaceExpressions component not found on the avatar!");
            Application.Quit();
        }

        if (!renderTexture)
        {
            Debug.LogError("Please assign a Render Texture to the script!");
            Application.Quit();
        }

        if (!videoComponent)
        {
            Debug.LogError("Please assign the Videos component!");
            Application.Quit();
        }

        movieRecorderSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movieRecorderSettings.name = "Video";
        movieRecorderSettings.Enabled = true;
        movieRecorderSettings.CaptureAudio = false;
        movieRecorderSettings.ImageInputSettings = new RenderTextureInputSettings
        {
            RenderTexture = renderTexture,
            OutputWidth = renderTexture.width,
            OutputHeight = renderTexture.height
        };

        var sessionTime = DateTimeOffset.Now;
        var folderName = $"{sessionTime.Year}{sessionTime.Month:D2}{sessionTime.Day:D2}-{sessionTime.Hour:D2}{sessionTime.Minute:D2}";

        sessionFolder = $"{Path.GetFullPath(Application.dataPath)}/../VideoRecordings/{folderName}";
        Directory.CreateDirectory(sessionFolder);
    }

    void Update()
    {
        if (!recording && action)
        {
            StartNewRecording();
        }
        else if (recording && !action)
        {
            StopRecording();
        }
    }

    private void StartNewRecording()
    {
        string recordName = (stage == VideoStage.Reactions) ? $"video_{emotion}" : $"ACT_{emotion}";
        movieRecorderSettings.OutputFile = Path.Combine(sessionFolder, recordName);

        var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        float videoFrameRate = videoComponent.GetVideoPlayer().frameRate;
        controllerSettings.AddRecorderSettings(movieRecorderSettings);
        controllerSettings.SetRecordModeToManual();
        controllerSettings.FrameRate = videoFrameRate;

        var recorderController = new RecorderController(controllerSettings);
        recorderController.PrepareRecording();

        if (recorderController.StartRecording())
        {
            StartRecordingExpressionWeights(recorderController, sessionFolder);
        }
        else
        {
            Debug.LogWarning("Not recording due to an internal error. Check previous message for more info. ");
        }
    }

    private void StartRecordingExpressionWeights(RecorderController recorderController, string folderPath)
    {
        string weightsName = (stage == VideoStage.Reactions) ? $"weights_{emotion}.csv" : $"ACT_{emotion}.csv";
        var weightFilePath = Path.Combine(folderPath, weightsName);

        var weightWriter = new StreamWriter(weightFilePath);

        // Write header row
        weightWriter.Write("Timestamp");

        foreach (var expression in expressionList)
        {
            weightWriter.Write(',');
            weightWriter.Write(expression.ToString());
        }

        weightWriter.WriteLine("");

        StartCoroutine(RecordExpressionWeights(recorderController, weightWriter));
    }

    private IEnumerator RecordExpressionWeights(RecorderController recorderController, StreamWriter weightWriter)
    {
        recording = true;
        var start = (int)(Time.time * 1000f);

        while (true)
        {
            // Write data every 100ms (10 samples per second)
            yield return new WaitForSeconds(.1f);

            // Stop if requested
            if (!recording)
            {
                recorderController.StopRecording();
                weightWriter.Close();
                break;
            }

            // Try again later if no valid records are found
            if (!faceExpressions.ValidExpressions) continue;

            // Get the elapsed time since recording start in milliseconds
            var dt = (int)(Time.time * 1000f - start);
            weightWriter.Write(dt);

            foreach (var expression in expressionList)
            {
                faceExpressions.TryGetFaceExpressionWeight(expression, out float weight);
                weightWriter.Write(',');
                weightWriter.Write(weight.ToString(CultureInfo.InvariantCulture));
            }

            weightWriter.WriteLine("");
        }
    }

    private void StopRecording()
    {
        recording = false;
    }

    void OnDisable()
    {
        StopRecording();
    }

    private static OVRFaceExpressions.FaceExpression[] GetFaceExpressions()
    {
        const int max = (int)OVRFaceExpressions.FaceExpression.Max;

        var exprs = new OVRFaceExpressions.FaceExpression[max];
        for (int i = 0; i < max; ++i)
            exprs[i] = (OVRFaceExpressions.FaceExpression)i;
        return exprs;
    }
}
