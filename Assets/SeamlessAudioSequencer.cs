using UnityEngine;

public class SeamlessAudioSequencer : MonoBehaviour
{
    public AudioSource introSource;
    public AudioSource loopSource;

    void Start()
    {
        // Define o tempo exato do clock de áudio para o início (0.1s a partir do Start)
        double startTime = AudioSettings.dspTime + 0.1;

        // Toca a intro no tempo agendado
        introSource.PlayScheduled(startTime);

        //Debug.Log($"Intro scheduled to play at: {startTime}, length: {introSource.clip.length}");

        // Agenda o segundo áudio para iniciar exatamente quando a intro terminar
        loopSource.loop = true;
        loopSource.PlayScheduled(startTime + (double)introSource.clip.length);
    }

    public void StopSequence()
    {
        if (introSource != null)
        {
            introSource.Stop();
        }

        if (loopSource != null)
        {
            loopSource.Stop();
        }

        enabled = false;
    }

    public void SetLoopSource(AudioSource audioSource)
    {
        this.loopSource = audioSource;
    }

    public void SetIntroSource(AudioSource audioSource)
    {
        this.introSource = audioSource;
    }

    public void StartSeamlessSound()
    {
        // Define o tempo exato do clock de áudio para o início (0.1s a partir do Start)
        double startTime = AudioSettings.dspTime + 0.1;

        if (introSource != null)
        {
            introSource.PlayScheduled(startTime);
        }
        
        // Agenda o segundo áudio para iniciar exatamente quando a intro terminar
        if (loopSource != null)
        {
            loopSource.loop = true;
            if (introSource != null)
            {
                loopSource.PlayScheduled(startTime + (double)introSource.clip.length);
            }
            else
            {
                // Se não houver intro, inicia o loop imediatamente
                loopSource.PlayScheduled(startTime);
            }
        }
    }

}