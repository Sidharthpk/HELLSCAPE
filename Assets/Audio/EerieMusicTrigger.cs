using UnityEngine;

// Walk into the trigger on the hell city's street and the eerie track (Song of Unhealing, or the Sound Library's
// "Hell City/Eerie music" slot) becomes the hell city's music. It goes through the MusicManager like every other
// track, so it crossfades out when another area's music starts (the office, a chase, the blood orb) and comes back
// when you return to the streets - never two songs at once.
public class EerieMusicTrigger : MonoBehaviour
{
    public AudioSource musicSource; // (old: it used to play on its own source; now only stopped if something left it playing)
    public AudioClip eerieTrack;
    public float fadeInDuration = 3f;
    public float volume = 0.35f;

    private bool triggered = false;

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;
        triggered = true;
        if (musicSource != null && musicSource.isPlaying) musicSource.Stop();
        var clip = Sounds.Clip("Hell City/Eerie music (street trigger)", eerieTrack);
        if (clip == null) return;
        // only take over if you're in the city (not mid-chase or in a cutscene area that has its own music)
        string here = MusicManager.CurrentArea;
        bool inCity = string.IsNullOrEmpty(here) || here == "Hell City";
        MusicManager.SetAreaMusic("Hell City", clip, volume, fadeInDuration, inCity);
    }
}
