using UnityEngine;
using UnityEngine.Playables;

public class CinematicTimelineTrigger : MonoBehaviour
{
    [SerializeField] PlayableDirector director;
    [SerializeField] bool playOnStart = false;
    [SerializeField] bool loop = false;

    bool playing;

    void Awake()
    {
        if (!director) director = GetComponent<PlayableDirector>();
    }

    void Start()
    {
        if (playOnStart) Play();
    }

    void Update()
    {
        if (!director) return;
        if (!playing) return;
        if (director.state != PlayState.Playing)
        {
            if (loop)
            {
                Play();
            }
            else
            {
                playing = false;
            }
        }
    }

    public void Play()
    {
        if (!director) return;
        director.time = 0;
        director.Play();
        playing = true;
    }

    public void Stop()
    {
        if (!director) return;
        director.Stop();
        playing = false;
    }
}
