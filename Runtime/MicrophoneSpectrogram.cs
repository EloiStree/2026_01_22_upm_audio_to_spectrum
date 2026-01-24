using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public class MicrophoneSpectrogram5s : MonoBehaviour
{
    [Header("Microphone")]
    public string microphoneName;
    public int sampleRate = 44100;

    [Header("Time Window")]
    public float secondsVisible = 5f;
    public int slicesPerSecond = 100;

    [Header("Frequency")]
    public int frequencyBins = 512;
    public FFTWindow fftWindow = FFTWindow.Blackman;

    [Header("Rendering")]
    public RawImage outputImage;
    public bool invertY = false;

    // Internal
    private AudioSource audioSource;
    private Texture2D spectrogramTexture;
    private float[,] spectrogramBuffer;
    private float[] spectrumData;

    private int timeWidth;
    private int writeColumn;

    private float sliceTimer;
    private float sliceInterval;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;

        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("No microphone detected.");
            enabled = false;
            return;
        }

        microphoneName = string.IsNullOrEmpty(microphoneName)
            ? Microphone.devices[0]
            : microphoneName;

        audioSource.clip = Microphone.Start(
            microphoneName,
            true,
            1,
            sampleRate
        );

        while (Microphone.GetPosition(microphoneName) <= 0) { }

        audioSource.Play();

        timeWidth = Mathf.RoundToInt(secondsVisible * slicesPerSecond);
        sliceInterval = 1f / slicesPerSecond;

        spectrogramBuffer = new float[timeWidth, frequencyBins];
        spectrumData = new float[frequencyBins];

        spectrogramTexture = new Texture2D(
            timeWidth,
            frequencyBins,
            TextureFormat.RGB24,
            false
        );

        spectrogramTexture.wrapMode = TextureWrapMode.Clamp;
        spectrogramTexture.filterMode = FilterMode.Point;

        if (outputImage != null)
            outputImage.texture = spectrogramTexture;
    }

    void Update()
    {
        sliceTimer += Time.deltaTime;

        while (sliceTimer >= sliceInterval)
        {
            sliceTimer -= sliceInterval;
            CaptureSpectrumSlice();
        }

        UpdateTexture();
    }

    void CaptureSpectrumSlice()
    {
        audioSource.GetSpectrumData(spectrumData, 0, fftWindow);

        for (int y = 0; y < frequencyBins; y++)
        {
            float value = spectrumData[y];
            float db = 20f * Mathf.Log10(value + 1e-7f); // dB scale
            spectrogramBuffer[writeColumn, y] = db;
        }

        writeColumn = (writeColumn + 1) % timeWidth;
    }

    void UpdateTexture()
    {
        for (int x = 0; x < timeWidth; x++)
        {
            int bufferX = (writeColumn + x) % timeWidth;

            for (int y = 0; y < frequencyBins; y++)
            {
                int drawY = invertY ? frequencyBins - 1 - y : y;

                float db = spectrogramBuffer[bufferX, y];

                // Map -80 dB → 0, 0 dB → 1
                float v = Mathf.InverseLerp(-80f, 0f, db);

                spectrogramTexture.SetPixel(x, drawY, SpectrogramColor(v));
            }
        }

        spectrogramTexture.Apply(false);
    }

    Color SpectrogramColor(float v)
    {
        // Simple heatmap
        return Color.Lerp(Color.black, Color.red, v);
    }
}
