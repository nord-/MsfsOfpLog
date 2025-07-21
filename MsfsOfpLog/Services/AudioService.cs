using System.Media;
using System.Reflection;

namespace MsfsOfpLog.Services
{
    public class AudioService : IDisposable
    {
        private readonly Dictionary<string, byte[]> _audioCache = new();
        private SoundPlayer? _currentPlayer;
        private readonly object _playLock = new();

        public AudioService()
        {
            LoadEmbeddedAudioFiles();
        }

        private void LoadEmbeddedAudioFiles()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames()
                .Where(name => name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase));

            foreach (var resourceName in resourceNames)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    var audioData = new byte[stream.Length];
                    stream.Read(audioData, 0, audioData.Length);
                    
                    // Extract just the filename without the full namespace path
                    var filename = resourceName.Split('.').TakeLast(2).First(); // Gets "v1", "100knots", "rotate"
                    _audioCache[filename] = audioData;
                    
                    Console.WriteLine($"🔊 Loaded audio: {filename}.wav ({audioData.Length} bytes)");
                }
            }
        }

        public void PlayAudio(string audioName)
        {
            try
            {
                lock (_playLock)
                {
                    if (_audioCache.TryGetValue(audioName, out var audioData))
                    {
                        // Stop any currently playing audio
                        _currentPlayer?.Stop();
                        _currentPlayer?.Dispose();

                        // Create a memory stream with the audio data
                        var memoryStream = new MemoryStream(audioData);
                        _currentPlayer = new SoundPlayer(memoryStream);
                        
                        // Play the audio asynchronously
                        _currentPlayer.Play();
                        
                        Console.WriteLine($"🔊 Playing audio: {audioName}.wav");
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ Audio file not found: {audioName}.wav");
                        Console.WriteLine($"Available audio files: {string.Join(", ", _audioCache.Keys.Select(k => k + ".wav"))}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error playing audio {audioName}: {ex.Message}");
            }
        }

        public void StopAudio()
        {
            lock (_playLock)
            {
                _currentPlayer?.Stop();
            }
        }

        public bool IsAudioAvailable(string audioName)
        {
            return _audioCache.ContainsKey(audioName);
        }

        public IEnumerable<string> GetAvailableAudioFiles()
        {
            return _audioCache.Keys.Select(k => k + ".wav");
        }

        public void Dispose()
        {
            lock (_playLock)
            {
                _currentPlayer?.Stop();
                _currentPlayer?.Dispose();
                _currentPlayer = null;
            }
        }
    }
}
