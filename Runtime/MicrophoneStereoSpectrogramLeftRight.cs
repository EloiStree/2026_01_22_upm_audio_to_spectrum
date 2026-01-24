using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public class MicrophoneStereoSpectrogramLeftRight : MonoBehaviour
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
    public int verticalResolution = 1024;
    public float magnitudeScale = 50f;

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
    private int visibleBins;

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

        float nyquist = sampleRate * 0.5f;

        minBin = Mathf.Clamp(
            Mathf.FloorToInt(minFrequency / nyquist * frequencyBins),
            0, frequencyBins - 1);

        maxBin = Mathf.Clamp(
            Mathf.CeilToInt(maxFrequency / nyquist * frequencyBins),
            0, frequencyBins - 1);

        visibleBins = maxBin - minBin + 1;

        leftBuffer = new float[timeWidth, visibleBins];
        rightBuffer = new float[timeWidth, visibleBins];

        leftSpectrum = new float[frequencyBins];
        rightSpectrum = new float[frequencyBins];

        spectrogramTexture = new Texture2D(
            timeWidth,
            verticalResolution * 2,
            TextureFormat.RGB24,
            false
        );

        spectrogramTexture.wrapMode = TextureWrapMode.Clamp;
        spectrogramTexture.filterMode = FilterMode.Point;

        outputImage.texture = spectrogramTexture;
    }

    void Update()
    {
        sliceTimer += Time.deltaTime;

        while (sliceTimer >= sliceInterval)
        {
            sliceTimer -= sliceInterval;
            CaptureAndDrawSlice();
        }
    }

    void CaptureAndDrawSlice()
    {
        audioSource.GetSpectrumData(leftSpectrum, 0, fftWindow);
        audioSource.GetSpectrumData(rightSpectrum, 1, fftWindow);

        // Cache FFT magnitudes
        for (int i = 0; i < visibleBins; i++)
        {
            int bin = minBin + i;
            leftBuffer[writeColumn, i] = leftSpectrum[bin] * magnitudeScale;
            rightBuffer[writeColumn, i] = rightSpectrum[bin] * magnitudeScale;
        }

        // Draw ONLY the current column
        for (int py = 0; py < verticalResolution; py++)
        {
            float t = py / (float)(verticalResolution - 1);
            float binPos = t * (visibleBins - 1);

            int b0 = Mathf.FloorToInt(binPos);
            int b1 = Mathf.Min(b0 + 1, visibleBins - 1);
            float f = binPos - b0;

            float leftV = Mathf.Lerp(
                leftBuffer[writeColumn, b0],
                leftBuffer[writeColumn, b1],
                f);

            float rightV = Mathf.Lerp(
                rightBuffer[writeColumn, b0],
                rightBuffer[writeColumn, b1],
                f);

            spectrogramTexture.SetPixel(
                writeColumn,
                py + verticalResolution,
                SpectrogramColor(Mathf.Clamp01(leftV))
            );

            spectrogramTexture.SetPixel(
                writeColumn,
                py,
                SpectrogramColor(Mathf.Clamp01(rightV))
            );
        }

        spectrogramTexture.Apply(false);

        // Advance cursor with wrap-around
        writeColumn = (writeColumn + 1) % timeWidth;
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



