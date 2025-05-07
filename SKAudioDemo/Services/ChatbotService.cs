using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using OpenAI.Chat;
using SKAudioDemo.Models;

namespace SKAudioDemo.Services
{
    public class ChatbotService
    {
        private readonly Kernel _kernel;
        private readonly ChatHistory _chatHistory;
        private readonly IConfiguration _config;
        private readonly string _apiKey;
        private readonly string _modelId;

        public ChatbotService(Kernel kernel, IConfiguration config)
        {
            _kernel = kernel;
            _chatHistory = new ChatHistory();
            _config = config;
            _apiKey = _config["OpenAI:ApiKey"] ?? "";
            _modelId = _config["OpenAI:ModelId"] ?? "gpt-4o-audio-preview";
        }

        public async Task<ChatResponse> SendAudioMessage(byte[] audioData, bool generateAudio = true)
        {
            if (audioData == null || audioData.Length == 0)
            {
                return new ChatResponse { TextResponse = "No audio data provided." };
            }

            try
            {
                var openAIClient = new OpenAI.OpenAIClient(_apiKey);

                string tempFile = Path.GetTempFileName() + ".wav";
                File.WriteAllBytes(tempFile, audioData);

                var transcriptionOptions = new OpenAI.Audio.AudioTranscriptionOptions
                {
                    Language = "en",
                };

                var transcriptionResult = await openAIClient.GetAudioClient("whisper-1").TranscribeAudioAsync(tempFile);
                string transcribedText = transcriptionResult.Value.Text;

                // Clean up the temporary file
                try { File.Delete(tempFile); } catch { }

                // If we got a transcription, process it with the chatbot
                if (!string.IsNullOrEmpty(transcribedText))
                {
                    Console.WriteLine($"Transcribed: {transcribedText}");
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                    return await SendMessage(transcribedText, generateAudio, new AudioContent(audioData, "audio/wav"));
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                }
                else
                {
                    return new ChatResponse { TextResponse = "Could not transcribe the audio. Please try again or type your message." };
                }
            }
            catch (Exception ex)
            {
                return new ChatResponse { TextResponse = $"Error processing audio: {ex.Message}" };
            }
        }

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        public async Task<ChatResponse> SendMessage(string message, bool generateAudio = false, AudioContent? audioContent = null)
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        {
            if (string.IsNullOrEmpty(message))
            {
                return new ChatResponse { TextResponse = "Please provide a message." };
            }

            _chatHistory.AddUserMessage(message);

            try
            {
                var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                var executionSettings = new AzureOpenAIPromptExecutionSettings
                {
                    Temperature = 0.7f,
                    TopP = 0.95f,
                    MaxTokens = 800,
                    ChatSystemPrompt = "You are a helpful, friendly, and knowledgeable assistant that specializes in answering questions about general topics. Be concise and clear in your responses.",
                    Modalities = ChatResponseModalities.Audio | ChatResponseModalities.Text
                };
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

                if (generateAudio)
                {
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only
                    executionSettings.Audio = new ChatAudioOptions(
                        ChatOutputAudioVoice.Alloy,
                        ChatOutputAudioFormat.Mp3

                    );

                    if (audioContent != null)
                    {
                        var chatMessageContents = new ChatMessageContentItemCollection();
                        chatMessageContents.Add(audioContent);
                        _chatHistory.AddUserMessage(chatMessageContents);
                    }
                    else
                    {
                        return new ChatResponse { TextResponse = "No audio provided." };
                    }

#pragma warning restore SKEXP0010
                }

                // Get completion from the service
                var result = await chatCompletionService.GetChatMessageContentAsync(
                    _chatHistory,
                    executionSettings,
                    _kernel);

                // Add the assistant's response to the chat history
                if (result.Content != null)
                {
                    _chatHistory.AddAssistantMessage(result.Content);
                }

                // Create response object
                var response = new ChatResponse
                {
                    TextResponse = result.Content ?? "No text response received."
                };

                // Handle audio content if present
                if (generateAudio && result.Items != null && result.Items.Count > 0)
                {
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only
                    foreach (var item in result.Items)
                    {
                        // Check if this is audio content
                        if (item is AudioContent aiAudioContent && aiAudioContent.Data.HasValue)
                        {
                            // Add the audio data to the response
                            response.AudioData = aiAudioContent.Data.Value.ToArray();
                            break; // Only handle the first audio content
                        }
                    }
#pragma warning restore SKEXP0001
                }

                return response;
            }
            catch (Exception ex)
            {
                return new ChatResponse { TextResponse = $"Error: {ex.Message}" };
            }
        }

        public void ClearChatHistory()
        {
            _chatHistory.Clear();
        }
    }
}
