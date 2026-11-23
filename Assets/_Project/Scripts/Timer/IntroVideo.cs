using UnityEngine;
using UnityEngine.Video;

public class IntroVideo : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public GameObject videoPanel;
    public GameObject gameplayUI;
    public GameTimer gameTimer;

    public GameObject skipHint;        // "Press SPACE to skip" yazısı
    public float hintDelay = 3f;       // yazı 3 saniyə sonra çıxsın

    public GameObject[] npcs;          // YENİ: bütün NPC-lər

    private bool finished;
    private float elapsed;

    void Start()
    {
        gameplayUI.SetActive(false);
        videoPanel.SetActive(true);
        if (skipHint != null) skipHint.SetActive(false);

        // YENİ: video gedərkən NPC-lər söndürülür
        foreach (var npc in npcs)
            npc.SetActive(false);

        videoPlayer.loopPointReached += OnVideoEnd;
        videoPlayer.Play();
    }

    void OnVideoEnd(VideoPlayer vp)
    {
        if (finished) return;
        finished = true;

        videoPlayer.loopPointReached -= OnVideoEnd;
        videoPlayer.Stop();

        if (skipHint != null) skipHint.SetActive(false);
        videoPanel.SetActive(false);
        gameplayUI.SetActive(true);

        // YENİ: video bitəndə NPC-lər aktiv olur
        foreach (var npc in npcs)
            npc.SetActive(true);

        gameTimer.StartTimer();
    }

    void Update()
    {
        if (finished) return;

        elapsed += Time.deltaTime;
        if (skipHint != null && elapsed >= hintDelay && !skipHint.activeSelf)
            skipHint.SetActive(true);

        if (Input.GetKeyDown(KeyCode.Space))
            OnVideoEnd(videoPlayer);
    }
}