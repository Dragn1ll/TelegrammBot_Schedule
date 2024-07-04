using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegrammBot_Schedule;

var botClient = new TelegramBotClient("6805957268:AAGn1Cy7hLnTI39GxWoPCacX_74Co2SM2GI");

using CancellationTokenSource cts = new CancellationTokenSource();

bool makeReminder = false;
Dictionary<long, GameState> games = new Dictionary<long, GameState>();
List<DelayedMessage> delayedMessages = new List<DelayedMessage>();

var buttons = new KeyboardButton[]
{
    "Игра", "Напоминалка"
};

SendDelayedMessagesAsync(cts.Token);
botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    pollingErrorHandler: HandlePollingErrorAsync,
    cancellationToken: cts.Token
);

Console.ReadLine();

cts.Cancel();


async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    if (update.Message is not { } message) return;
    if (message.Text is not { } messageText) return;

    await Console.Out.WriteLineAsync($"{message.Chat.Id} wrote {messageText}");

    if (games.ContainsKey(message.Chat.Id))
        await GuessNumberGameAsync(botClient, message, games[message.Chat.Id]);
    else if (messageText.ToLower() == "игра")
    {
        await botClient.SendTextMessageAsync(message.Chat.Id, "Приветствую в игре: Угадай число!",
            replyMarkup: new ReplyKeyboardRemove());
        await botClient.SendTextMessageAsync(message.Chat.Id, "Правила игры:\n" +
                                                "- Вам дается 3 попытки угадать число от 1 до 10.\n" +
                                                "- Я буду давать подсказки (больше/меньше).\n" +
                                                "Удачи!");
        games.Add(message.Chat.Id, new GameState(message.Chat.Id));
        await botClient.SendTextMessageAsync(message.Chat.Id, "Загадал число. Ваш первый ход:");
    }
    else if (makeReminder)
        await MakeReminderAsync(message);
    else if (messageText.ToLower() == "напоминалка")
    {
        await botClient.SendTextMessageAsync(message.Chat.Id, "Введите дату и сообщение в формате(время пишите по МСК):\n" +
            "гггг.мм.дд|чч:мм|сообщение\n" +
            "Пример: 2005.3.31|6:05|С днём рождения!",
            replyMarkup: new ReplyKeyboardRemove());
        makeReminder = true;
    }
    else
    {
        await botClient.SendTextMessageAsync(message.Chat.Id, $"Вы сказали: {messageText}",
            replyMarkup: new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true });
    }
}

async Task MakeReminderAsync(Message message)
{
    var settings = message.Text!.Split('|');

    try
    {
        if (settings.Length == 3)
        {
            var date = settings[0].Split(".").Select(s => int.Parse(s)).ToArray();
            var time = settings[1].Split(":").Select(s => int.Parse(s)).ToArray();
            DateTime dateMesage = new DateTime(date[0], date[1], date[2], time[0], time[1], 0);
            DelayedMessage tmpMessage = new DelayedMessage(dateMesage, message.Chat.Id, settings[2]);

            delayedMessages.Add(tmpMessage);

            await botClient.SendTextMessageAsync(message.Chat.Id, "Напоминалка создана!",
                replyMarkup: new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true });
            makeReminder = false;
        }
        else
            await botClient.SendTextMessageAsync(message.Chat.Id, "Вы что-то не дописали.\n" +
            "Введите её заново, но в этот раз правильно:");
    }
    catch
    {
        await botClient.SendTextMessageAsync(message.Chat.Id, "Вы записи сверху допущена ошибка.\n" +
            "Введите её заново, но в этот раз правильно:");
    }
    
}

async Task SendDelayedMessagesAsync(CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        if (delayedMessages != null && delayedMessages.OrderBy(m => m.DateTime).First().DateTime
                                                                                    <= DateTime.Now)
        {
            var message = delayedMessages.OrderBy(m => m.DateTime).First();
            await botClient.SendTextMessageAsync(message.ChatId, message.Text);
            delayedMessages.Remove(message);
        }
    }
}

async Task GuessNumberGameAsync(ITelegramBotClient botClient, Message message, GameState game)
{
    if (!int.TryParse(message.Text, out int userNum)) return;

    if (userNum == game.RandomNumber)
    {
        await botClient.SendTextMessageAsync(message.Chat.Id, "Поздравляю! Вы угадали!",
            replyMarkup: new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true });
        
        games.Remove(message.Chat.Id);
    }
    else if (userNum > game.RandomNumber)
    {
        await botClient.SendTextMessageAsync(message.Chat.Id, "Загаданное число меньше.\n" +
                                            $"У вас осталось попыток: {--game.RemainingAttempts}");
    }
    else
    {
        await botClient.SendTextMessageAsync(message.Chat.Id, "Загаданное число больше.\n" +
                                            $"У вас осталось попыток: {--game.RemainingAttempts}");
    }

    if (game.RemainingAttempts == 0)
    {
        await botClient.SendTextMessageAsync(message.Chat.Id, "К сожалению, вы проиграли.\n" +
                                            $"Загаданное число было: {game.RandomNumber}",
                                            replyMarkup: new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true });

        games.Remove(message.Chat.Id);
    }
}

Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    var errorMessage = exception switch
    {
        ApiRequestException apiRequestException =>
            $"Ошибка Telegram API:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };

    Console.WriteLine(errorMessage);
    return Task.CompletedTask;
}
