using UnityEngine;
using System;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class WebcamMicSpectrum : MonoBehaviour
{
    [Header("Microphone Discovery")]
    public List<string> m_microphones = new List<string>();

    public List<string> m_microphonePriorityList = new List<string>()
    {
        "Built-in",
        "USB",
        "Headset"
    };

    [Header("Microphone Settings")]
    public string microphoneName;
    public int sampleRate = 44100;

    [Header("Spectrum Settings")]
    public int spectrumSize = 1024;
    public FFTWindow fftWindow = FFTWindow.Blackman;

    public float[] spectrumData;

    public event Action<float[]> OnSpectrumUpdated;

    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;

        ListMicrophones();

        if (m_microphones.Count == 0)
        {
            Debug.LogError("No microphones detected. The void remains silent.");
            enabled = false;
            return;
        }

        microphoneName = SelectMicrophoneByPriority();

        Debug.Log("Using microphone: " + microphoneName);

        audioSource.clip = Microphone.Start(
            microphoneName,
            true,
            1,
            sampleRate
        );

        while (Microphone.GetPosition(microphoneName) <= 0) { }

        audioSource.Play();

        spectrumData = new float[spectrumSize];
    }

    void Update()
    {
        if (!audioSource.isPlaying) return;

        audioSource.GetSpectrumData(spectrumData, 0, fftWindow);
        OnSpectrumUpdated?.Invoke(spectrumData);
    }

    void ListMicrophones()
    {
        Debug.Log("Detected Microphone Devices:");

        foreach (var device in Microphone.devices)
        {
            Debug.Log(device);
            m_microphones.Add(device);
        }
    }

    string SelectMicrophoneByPriority()
    {
        // Try priority keywords first
        foreach (var priority in m_microphonePriorityList)
        {
            foreach (var mic in m_microphones)
            {
                if (mic.IndexOf(priority, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.Log($"Priority match found: {mic}");
                    return mic;
                }
            }
        }

        // Fallback
        Debug.Log("No priority match found. Falling back to first device.");
        return m_microphones[0];
    }
}
