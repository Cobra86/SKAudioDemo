using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using NAudio.Wave;
using SKAudioDemo.Plugins;
using SKAudioDemo.Services;

class Program
{
    static async Task Main(string[] args)
    {
        var configBuilder = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables();
        var config = configBuilder.Build();

        var host = Host.CreateDefaultBuilder(args)
               .ConfigureServices((context, services) =>
               {
                   services.AddSingleton<IConfiguration>(config);

                   services.AddSingleton<Kernel>(sp =>
                   {
                       var builder = Kernel.CreateBuilder();

                       string modelId = config["OpenAI:ModelId"] ?? "gpt-4o-audio-preview";
                       string apiKey = config["OpenAI:ApiKey"] ?? "";

                       builder.AddOpenAIChatCompletion(
                           modelId,
                           apiKey);

                       builder.Plugins.AddFromObject(new WeatherPlugin());
                       builder.Plugins.AddFromObject(new CalculatorPlugin());

                       return builder.Build();
                   });

                   services.AddSingleton<ChatbotService>();
               })
               .Build();

        var chatbotService = host.Services.GetRequiredService<ChatbotService>();

        bool canRecord = true;
        try
        {
            var waveIn = new WaveInEvent();
            waveIn.Dispose();
        }
        catch
        {
            canRecord = false;
            Console.WriteLine("Audio recording is not available on this system.");
        }

        bool useAudio = false;

        if (canRecord)
        {
            Console.WriteLine("Would you like to use audio input/output? (y/n)");
            useAudio = Console.ReadLine()?.ToLower() == "y";
        }

        Console.WriteLine("\n===== Semantic Kernel Chatbot Demo =====");
        Console.WriteLine("Type 'exit' or 'quit' to end the conversation.");
        Console.WriteLine("Type 'clear' to clear the conversation history.");
        Console.WriteLine("=========================================\n");

        while (true)
        {
            string userMessage;

            if (useAudio)
            {
                Console.WriteLine("Press Enter to start recording (speak for up to 10 seconds, or press Enter again to stop)...");
                Console.ReadLine();

                try
                {
                    byte[] audioBytes = RecordAudioFromMicrophone();
                    Console.WriteLine("Recording complete. Processing...");

                    if (audioBytes != null && audioBytes.Length > 0)
                    {
                        // Send audio directly to the service for transcription and processing
                        var response = await chatbotService.SendAudioMessage(audioBytes);
                        Console.WriteLine();

                        if (response == null || string.IsNullOrEmpty(response.TextResponse))
                        {
                            Console.WriteLine("Assistant > No response received. Please try again.");
                            continue;
                        }

                        Console.WriteLine($"Assistant > {response.TextResponse}");

                        if (useAudio && response.AudioData != null && response.AudioData.Length > 0)
                        {
                            string tempAudioFile = "assistant_response.mp3";
                            File.WriteAllBytes(tempAudioFile, response.AudioData);
                            Console.WriteLine("Playing audio response...");
                            PlayAudioFile(tempAudioFile);
                        }

                        Console.WriteLine("-----------------------");
                        continue;
                    }
                    else
                    {
                        Console.WriteLine("No audio recorded. Please type your question:");
                        userMessage = Console.ReadLine() ?? "";
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error recording audio: {ex.Message}");
                    Console.WriteLine("Please type your question instead:");
                    userMessage = Console.ReadLine() ?? "";
                }
            }
            else
            {
                Console.Write("User > ");
                userMessage = Console.ReadLine() ?? "";
            }

            if (string.IsNullOrEmpty(userMessage))
                continue;

            if (userMessage.ToLower() == "exit" || userMessage.ToLower() == "quit")
                break;

            if (userMessage.ToLower() == "clear")
            {
                chatbotService.ClearChatHistory();
                Console.WriteLine("Conversation history cleared.");
                continue;
            }

            try
            {
                var audioEnabled = useAudio;

                var response = await chatbotService.SendMessage(userMessage, audioEnabled);
                Console.WriteLine();

                if (response == null || string.IsNullOrEmpty(response.TextResponse))
                {
                    Console.WriteLine("Assistant > No response received. Please try again.");
                    continue;
                }

                Console.WriteLine($"Assistant > {response.TextResponse}");

                if (useAudio && response.AudioData != null && response.AudioData.Length > 0)
                {
                    string tempAudioFile = "assistant_response.mp3";
                    File.WriteAllBytes(tempAudioFile, response.AudioData);
                    Console.WriteLine("Playing audio response...");
                    PlayAudioFile(tempAudioFile);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing request: {ex.Message}");
            }

            Console.WriteLine("-----------------------");
        }

        Console.WriteLine("Thank you for using the Semantic Kernel Chatbot Demo. Goodbye!");
    }

    static byte[] RecordAudioFromMicrophone(int recordingTimeSeconds = 10)
    {
        try
        {
            using (var waveIn = new WaveInEvent())
            {
                waveIn.WaveFormat = new WaveFormat(16000, 1);

                var buffer = new MemoryStream();
                var writer = new WaveFileWriter(buffer, waveIn.WaveFormat);

                var recordingCompleted = new System.Threading.ManualResetEvent(false);
                bool stopRequested = false;

                waveIn.DataAvailable += (sender, e) =>
                {
                    if (stopRequested) return;
                    writer.Write(e.Buffer, 0, e.BytesRecorded);
                };

                waveIn.RecordingStopped += (sender, e) =>
                {
                    writer.Flush();
                    recordingCompleted.Set();
                };

                // Start recording
                waveIn.StartRecording();
                Console.WriteLine("Recording... (press Enter to stop)");

                var userInputTask = Task.Run(() =>
                {
                    Console.ReadLine();
                    stopRequested = true;
                    waveIn.StopRecording();
                });

                var timerTask = Task.Delay(recordingTimeSeconds * 1000).ContinueWith(t =>
                {
                    if (!stopRequested)
                    {
                        stopRequested = true;
                        waveIn.StopRecording();
                    }
                });

                recordingCompleted.WaitOne();

                writer.Dispose();
                return buffer.ToArray();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error recording audio: {ex.Message}");
            return null;
        }
    }

    static void PlayAudioFile(string filePath)
    {
        try
        {
            using (var audioFile = new MediaFoundationReader(filePath))
            using (var outputDevice = new WaveOutEvent())
            {
                Console.WriteLine("Starting audio playback...");
                outputDevice.Init(audioFile);
                outputDevice.Play();

                while (outputDevice.PlaybackState == PlaybackState.Playing)
                {
                    System.Threading.Thread.Sleep(100);
                }
                Console.WriteLine("Audio playback completed.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error playing audio: {ex.Message}");
            try
            {
                Console.WriteLine("Attempting to play with system default player...");
                var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = filePath;
                process.StartInfo.UseShellExecute = true;
                process.Start();
                System.Threading.Thread.Sleep(5000);
            }
            catch (Exception fallbackEx)
            {
                Console.WriteLine($"Fallback playback failed: {fallbackEx.Message}");
            }
        }
    }
}

