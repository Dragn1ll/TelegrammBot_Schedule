namespace TelegrammBot_Schedule;

[Serializable]
public class DelayedMessage
{
    public DateTime DateTime { get; }
    public long ChatId { get; }
    public string Text { get; }

    public DelayedMessage(DateTime dateTime, long chatId, string message)
    {
        DateTime = dateTime;
        ChatId = chatId;
        Text = message;
    }
}
