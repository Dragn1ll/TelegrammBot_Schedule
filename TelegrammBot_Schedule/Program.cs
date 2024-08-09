using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegrammBot_Schedule;

var botClient = new TelegramBotClient("6805957268:AAGn1Cy7hLnTI39GxWoPCacX_74Co2SM2GI");

using CancellationTokenSource cts = new CancellationTokenSource();

FileStream fsReminder = new FileStream("DelayedMessages.json", FileMode.OpenOrCreate);
SendReminderAsync(JsonSerializer.Deserialize<List<DelayedMessage>>(fsReminder));
fsReminder.SetLength(0);
JsonSerializer.Serialize(fsReminder, new List<DelayedMessage>());
fsReminder.Close();
List<DelayedMessage> makeReminder = new List<DelayedMessage>();

FileStream fsGames = new FileStream("Games.json", FileMode.OpenOrCreate);
Dictionary<long, GameState>? games = JsonSerializer.Deserialize<Dictionary<long, GameState>>(fsGames);
fsGames.Close();

var buttons = new KeyboardButton[]
{
    "Игра", "Напоминалка"
};

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

    if (games!.ContainsKey(message.Chat.Id))
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

        using (FileStream fsGames = new FileStream("Games.json", FileMode.OpenOrCreate))
        {
            fsGames.SetLength(0);
            await JsonSerializer.SerializeAsync<Dictionary<long, GameState>>(fsGames, games);
        }
        await botClient.SendTextMessageAsync(message.Chat.Id, "Загадал число. Ваш первый ход:");
    }
    else if (makeReminder.Where(r => r.ChatId == message.Chat.Id && r.Text == null).Count() == 1)
    {
        var tmp = makeReminder
            .Where(r => r.ChatId == message.Chat.Id && r.Text == null)
            .First();
        tmp.Text = messageText;
        MakeReminderAsync(tmp);
    }
    else if (messageText.ToLower() == "напоминалка")
    {
        await botClient.SendTextMessageAsync(message.Chat.Id, "Введите дату и сообщение в формате(время пишите по МСК):\n" +
            "гггг.мм.дд|чч:мм|сообщение\n" +
            "Пример: 2005.3.31|6:05|С днём рождения!",
            replyMarkup: new ReplyKeyboardRemove());
        makeReminder.Add(new DelayedMessage(message.Chat.Id));
    }
    else
    {
        await botClient.SendTextMessageAsync(message.Chat.Id, $"Вы сказали: {messageText}",
            replyMarkup: new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true });
    }
}

async Task MakeReminderAsync(DelayedMessage message)
{
    var settings = message.Text!.Split('|');

    try
    {
        if (settings.Length == 3)
        {
            var date = settings[0].Split(".").Select(s => int.Parse(s)).ToArray();
            var time = settings[1].Split(":").Select(s => int.Parse(s)).ToArray();
            message.Text = settings[2];
            message.DateTime = new DateTime(date[0], date[1], date[2], time[0], time[1], 0);

            if (message.DateTime <= DateTime.Now)
            {
                await botClient.SendTextMessageAsync(message.ChatId, "Время слишком раннее." +
                    " Перепишите запрос, пожалуйста.");
                return;
            }
            
            await botClient.SendTextMessageAsync(message.ChatId, "Напоминалка создана! Ожидайте.",
                replyMarkup: new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true });

            await Task.Delay(message.DateTime - DateTime.Now);
            makeReminder.Remove(message);
            await botClient.SendTextMessageAsync(message.ChatId, message.Text);

        }
        else
            await botClient.SendTextMessageAsync(message.ChatId, "Вы что-то не дописали или время раньше нынешнего.\n" +
            "Введите её заново, но в этот раз правильно:");
    }
    catch
    {
        await botClient.SendTextMessageAsync(message.ChatId, "В записи сверху допущена ошибка.\n" +
            "Введите её заново, но в этот раз правильно:");
    }
}

async Task SendReminderAsync(List<DelayedMessage> reminders)
{
    reminders.OrderBy(r => r.DateTime);
    foreach (var reminder in reminders)
    {
        if (reminder.DateTime <= DateTime.Now)
        {
            await botClient.SendTextMessageAsync(reminder.ChatId, $"Простите, " +
                $"если опоздали с напоминанием:\n{reminder.Text}");
            reminders.Remove(reminder);
        }
        else
        {
            await Task.Delay(reminder.DateTime - DateTime.Now);
            reminders.Remove(reminder);
            await botClient.SendTextMessageAsync(reminder.ChatId, reminder.Text);
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

    using (FileStream fsGames = new FileStream("Games.json", FileMode.OpenOrCreate))
    {
        fsGames.SetLength(0);
        await JsonSerializer.SerializeAsync<Dictionary<long, GameState>>(fsGames, games);
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