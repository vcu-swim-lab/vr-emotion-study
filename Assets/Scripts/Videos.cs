using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;


public enum Emotion
{
    Anger,
    Disgust,
    Fear,
    Happiness,
    Neutral,
    Sadness,
    Surprise,
    Max,
}


internal class VideoList
{
    public VideoList(string folder)
    {
        this.folder = folder;

        const int max = (int)Emotion.Max;
        emotions = new Emotion[max];

        for (int i = 0; i < max; ++i)
            emotions[i] = (Emotion)i;

        // shuffle the emotions for fairness
        for (int i = max - 1; i > 0; --i)
        {
            var j = Random.Range(0, i);
            (emotions[j], emotions[i]) = (emotions[i], emotions[j]);
        }
    }

    public string Next(out Emotion emotion)
    {
        if (current == emotions.Length)
        {
            // NOTE: this should never happen, but just in case...
            emotion = Emotion.Max;
            return null;
        }

        emotion = emotions[current++];
        return $"{folder}/{emotion}.mp4";
    }

    public int Count { get => emotions.Length - current; }

    private readonly string folder;
    private readonly Emotion[] emotions;
    private int current = 0;
}

public class Videos : MonoBehaviour
{
    //Variable to hold "Video Player" component. Assigned in start function
    private VideoPlayer videoPlayer;
    private AudioSource vrAudioSource;

    //Button on the intermediary screen used to start next video
    [SerializeField] private Button nextButton;
    private bool nextClicked = false;

    // The collection of videos to play on the first part.
    private VideoList videos;
    // The collection of videos to play on the second part.
    private VideoList emotionsText;


    void Awake()
    {
        videos = new VideoList(VIDEOS_FOLDER);
        emotionsText = new VideoList(TEXT_VIDEOS_FOLDER);

        //Assign videoPlayer to the "Video Player" component of the screen GameObject
        videoPlayer = GetComponent<VideoPlayer>();

        //Assign vrAudioSource to the "Audio Player" component of the screen
        vrAudioSource = GetComponent<AudioSource>();

        // Configure the VideoPlayer to output audio to the AudioSource
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.SetTargetAudioSource(0, vrAudioSource);

        // Allow this audio source to ignore the global AudioListener pause
        vrAudioSource.ignoreListenerPause = true;

        StartCoroutine(AnimationCoro());

        // notify the animation coroutine that the button was clicked
        nextButton.onClick.AddListener(() =>
        {
            nextClicked = true;
            nextButton.gameObject.SetActive(false);
        });
    }

    public VideoPlayer GetVideoPlayer() => videoPlayer;

    private IEnumerator AnimationCoro()
    {
        yield return StartCoroutine(AwaitButtonClick());

        //play videos
        do
        {
            yield return StartCoroutine(PlayNextVideo(videos));
            yield return StartCoroutine(AwaitButtonClick());
        }
        while (videos.Count > 0);

        // Play the clip that explains the acting stage and wait for it to finish
        yield return StartCoroutine(PlayIntroToActing());
        yield return StartCoroutine(AwaitButtonClick());

        Record.stage = VideoStage.Texts;

        //play acting phase prompts
        do
        {
            yield return StartCoroutine(PlayNextVideo(emotionsText));
            yield return StartCoroutine(AwaitButtonClick());
        }
        while (emotionsText.Count > 0);

        //shut down the program
        Application.Quit();
    }

    //Function to choose and play the next video 
    private IEnumerator PlayNextVideo(VideoList list)
    {
        //The video to play will be in the list at the index chosen above
        var videoPath = list.Next(out var emotion);

        //Determine emotion part of filename
        Record.emotion = emotion;

        //Assign chosen video to videoPlayer
        videoPlayer.url = videoPath;

        //Play
        videoPlayer.Play();
        yield return new WaitUntil(() => videoPlayer.isPlaying); // wait for the video player to start so then we can start recording

        //Start recording Aura
        Record.action = true;

        yield return new WaitUntil(() => !videoPlayer.isPlaying);

        Record.action = false;
    }

    private IEnumerator PlayIntroToActing()
    {
        videoPlayer.url = INTRO_PATH;
        videoPlayer.Play();

        // Wait for the video player to start so then we can wait for it to finish later.
        // If we await for the video to finish without starting first, it would complete before the video even starts.
        yield return new WaitUntil(() => videoPlayer.isPlaying);

        // wait for the video player to finish
        yield return new WaitUntil(() => !videoPlayer.isPlaying);
    }

    private IEnumerator AwaitButtonClick()
    {
        nextClicked = false;
        nextButton.gameObject.SetActive(true);

        yield return new WaitUntil(() => nextClicked);
    }


    private static string VIDEOS_FOLDER => $"{Application.dataPath}/Videos/Emotions";

    private static string INTRO_PATH => $"{Application.dataPath}/Videos/Intro.mp4";

    private static string TEXT_VIDEOS_FOLDER => $"{Application.dataPath}/Videos/EmotionTextVids";
}
