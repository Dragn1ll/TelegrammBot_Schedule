namespace TelegrammBot_Schedule;

[Serializable]
public class DelayedMessage(long chatId)
{
    public long ChatId { get; } = chatId;
    public DateTime DateTime { get; set; }
    public string Text { get; set; }
}
