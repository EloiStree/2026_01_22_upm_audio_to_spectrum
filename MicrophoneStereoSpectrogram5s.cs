using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public class MicrophoneStereoSpectrogram5s : MonoBehaviour
{
    [Header("Microphone")]
    public string microphoneName;
    public int sampleRate = 44100;

    [Header("Time Window")]
    public float secondsVisible = 5f;
    public int slicesPerSecond = 100;

    [Header("Frequency")]
    public int frequencyBins = 512;
    public FFTWindow fftWindow = FFTWindow.Hanning;
    public float minFrequency = 20f;
    public float maxFrequency = 6000f;

    [Header("Rendering")]
    public RawImage outputImage;
    public float magnitudeScale = 50f; // Web Audio–style gain

    private AudioSource audioSource;
    private Texture2D spectrogramTexture;

    private float[,] leftBuffer;
    private float[,] rightBuffer;

    private float[] leftSpectrum;
    private float[] rightSpectrum;

    private int timeWidth;
    private int writeColumn;

    private float sliceTimer;
    private float sliceInterval;

    private int minBin;
    private int maxBin;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("No microphone detected.");
            enabled = false;
            return;
        }

        microphoneName = string.IsNullOrEmpty(microphoneName)
            ? Microphone.devices[0]
            : microphoneName;

        audioSource.clip = Microphone.Start(microphoneName, true, 1, sampleRate);

        while (Microphone.GetPosition(microphoneName) <= 0) { }

        audioSource.Play();

        timeWidth = Mathf.RoundToInt(secondsVisible * slicesPerSecond);
        sliceInterval = 1f / slicesPerSecond;

        leftBuffer = new float[timeWidth, frequencyBins];
        rightBuffer = new float[timeWidth, frequencyBins];

        leftSpectrum = new float[frequencyBins];
        rightSpectrum = new float[frequencyBins];

        spectrogramTexture = new Texture2D(
            timeWidth,
            frequencyBins * 2,
            TextureFormat.RGB24,
            false
        );

        spectrogramTexture.wrapMode = TextureWrapMode.Clamp;
        spectrogramTexture.filterMode = FilterMode.Point;

        outputImage.texture = spectrogramTexture;

        float nyquist = sampleRate * 0.5f;

        minBin = Mathf.FloorToInt(minFrequency / nyquist * frequencyBins);
        maxBin = Mathf.CeilToInt(maxFrequency / nyquist * frequencyBins);

        minBin = Mathf.Clamp(minBin, 0, frequencyBins - 1);
        maxBin = Mathf.Clamp(maxBin, 0, frequencyBins - 1);
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
        audioSource.GetSpectrumData(leftSpectrum, 0, fftWindow);
        audioSource.GetSpectrumData(rightSpectrum, 1, fftWindow);

        for (int y = 0; y < frequencyBins; y++)
        {
            if (y < minBin || y > maxBin)
            {
                leftBuffer[writeColumn, y] = 0f;
                rightBuffer[writeColumn, y] = 0f;
                continue;
            }

            // Raw magnitude, Web Audio–style
            leftBuffer[writeColumn, y] = leftSpectrum[y] * magnitudeScale;
            rightBuffer[writeColumn, y] = rightSpectrum[y] * magnitudeScale;
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
                float leftV = Mathf.Clamp01(leftBuffer[bufferX, y]);
                float rightV = Mathf.Clamp01(rightBuffer[bufferX, y]);

                spectrogramTexture.SetPixel(
                    x,
                    y + frequencyBins,
                    SpectrogramColor(leftV)
                );

                spectrogramTexture.SetPixel(
                    x,
                    y,
                    SpectrogramColor(rightV)
                );
            }
        }

        spectrogramTexture.Apply(false);
    }

    Color SpectrogramColor(float v)
    {
        if (v < 0.33f)
            return Color.Lerp(Color.black, Color.blue, v / 0.33f);

        if (v < 0.66f)
            return Color.Lerp(Color.blue, Color.green, (v - 0.33f) / 0.33f);

        return Color.Lerp(Color.green, Color.red, (v - 0.66f) / 0.34f);
    }
}
