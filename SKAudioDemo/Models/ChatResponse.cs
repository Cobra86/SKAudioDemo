namespace SKAudioDemo.Models
{
    public class ChatResponse
    {
        public string TextResponse { get; set; } = string.Empty;
        public byte[]? AudioData { get; set; }
    }
}
