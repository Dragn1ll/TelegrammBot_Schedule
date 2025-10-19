namespace TelegramBot_Schedule;

[Serializable]
public class GameState
{
    public int RandomNumber { get; }
    public int RemainingAttempts { get; set; }
    public long ChatId { get; }

    public GameState(long chatId)
    {
        RandomNumber = new Random().Next(1, 11);
        RemainingAttempts = 3;
        ChatId = chatId;
    }
}
