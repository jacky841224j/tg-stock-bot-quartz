using Microsoft.Playwright;
using Quartz;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TG_Stock_Bot
{
    [DisallowConcurrentExecution]

    public class tg_message : IJob
    {
        #region 基本參數
        //Time
        int year;
        int month;
        int day;
        int hour;
        int minute;
        int second;

        //Messages and user info
        long chatId = 0;
        string messageText;
        int messageId;
        string firstName;
        string lastName;
        long id;
        Message sentMessage;
        int StockNumber;

        //股價資訊
        Dictionary<int, string> InfoDic = new Dictionary<int, string>()
            {
               { 0, "開盤價"},{ 1, "最高價"},{ 2, "成交量"},
               { 3, "昨日收盤價"},{ 4, "最低價"},{ 5, "成交額"},
               { 6, "均價"},{ 7, "本益比"},{ 8, "市值"},
               { 9, "振幅"},{ 10, "周轉率"},{ 11, "發行股"},
               { 12, "漲停"},{ 13, "52W高"},{ 14, "內盤量"},
               { 15, "跌停"},{ 16, "52W低"},{ 17, "外盤量"},
               { 18, "近四季EPS"},{ 19, "當季EPS"},{ 20, "毛利率"},
               { 21, "每股淨值"},{ 22, "本淨比"},{ 23, "營利率"},
               { 24, "年股利"},{ 25, "殖利率"},{ 26, "淨利率"},
            };

        #endregion

        public async Task Execute(IJobExecutionContext context1)
        {
            //Read time and save variables
            year = int.Parse(DateTime.UtcNow.Year.ToString());
            month = int.Parse(DateTime.UtcNow.Month.ToString());
            day = int.Parse(DateTime.UtcNow.Day.ToString());
            hour = int.Parse(DateTime.UtcNow.Hour.ToString());
            minute = int.Parse(DateTime.UtcNow.Minute.ToString());
            second = int.Parse(DateTime.UtcNow.Second.ToString());

            Console.WriteLine("Data: " + year + "/" + month + "/" + day);
            Console.WriteLine("Time: " + hour + ":" + minute + ":" + second);

            #region 設定TG_BOT
            //Bot
            var botClient = new TelegramBotClient("1546506272:AAFeRnuJOsjfxYIo_LcsSOMRgIfl24v5fzY");

            var cts = new CancellationTokenSource();

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = { } // receive all update types
            };
            botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken: cts.Token);

            var me = await botClient.GetMeAsync();

            Console.WriteLine($"\nHello! I'm {me.Username} and i'm your Bot!");

            #endregion

            async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
            {
                try
                {
                    // Only process Message updates: https://core.telegram.org/bots/api#message
                    if (update.Type != UpdateType.Message)
                        return;
                    // Only process text messages
                    if (update.Message!.Type != MessageType.Text)
                        return;

                    #region 初始化參數
                    chatId = update.Message.Chat.Id;
                    messageText = update.Message.Text;
                    messageId = update.Message.MessageId;
                    firstName = update.Message.From.FirstName;
                    lastName = update.Message.From.LastName;
                    id = update.Message.From.Id;
                    year = update.Message.Date.Year;
                    month = update.Message.Date.Month;
                    day = update.Message.Date.Day;
                    hour = update.Message.Date.Hour;
                    minute = update.Message.Date.Minute;
                    second = update.Message.Date.Second;

                    Console.WriteLine(" message --> " + year + "/" + month + "/" + day + " - " + hour + ":" + minute + ":" + second);
                    Console.WriteLine($"Received a '{messageText}' message in chat {chatId} from user:\n" + firstName + " - " + lastName + " - " + " 5873853");

                    messageText = messageText.ToLower();
                    if (messageText == "/start" || messageText == "hello")
                    {
                        // Echo received message text
                        sentMessage = await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "Hello " + firstName + " " + lastName + "",
                        cancellationToken: cancellationToken);
                        return;
                    }
                    else if (messageText.Split().ToList().Count < 2) return;

                    var text = messageText.Split().ToList();

                    int.TryParse(text[1], out StockNumber);
                    //if (!int.TryParse(text[1], out StockNumber)) return;

                    #endregion

                    #region 建立瀏覽器

                    using var playwright = await Playwright.CreateAsync();

                    await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        ExecutablePath = "/root/.cache/ms-playwright/chromium-1055/chrome-linux/chrome",
                        Args = new[] {
                            "--disable-dev-shm-usage",
                            "--disable-setuid-sandbox",
                            "--no-sandbox"
                        },
                        Headless = true,
                        ChromiumSandbox = false,
                    });
                    var context = await browser.NewContextAsync();
                    var page = await browser.NewPageAsync();
                    await page.SetViewportSizeAsync(1920, 1080);
                    Console.WriteLine($"Browser is Setting");
                    #endregion

                    if (messageText.Contains("/url"))
                    {
                        if (text.Count != 2) return;

                        Console.WriteLine($"讀取網站中...");
                        await page.GotoAsync($"{text[1]}",
                            new PageGotoOptions { WaitUntil = WaitUntilState.Load }).WaitAsync(new TimeSpan(0, 1, 0));
                        Console.WriteLine($"存取圖片中...");
                        Stream stream = new MemoryStream(await page.ScreenshotAsync());
                        sentMessage = await botClient.SendPhotoAsync(
                        chatId: chatId,
                        photo: stream,
                        parseMode: ParseMode.Html,
                        cancellationToken: cancellationToken);
                        return;
                    }

                    #region Google

                    if (messageText.Contains("/google"))
                    {
                        if (text.Count != 2) return;

                        Console.WriteLine($"讀取網站中...");
                        var url = $"https://www.google.com/finance/quote/{StockNumber}:TPE";
                        await page.GotoAsync(url,
                                new PageGotoOptions { WaitUntil = WaitUntilState.Load }).WaitAsync(new TimeSpan(0, 1, 0));
                        Console.WriteLine($"擷取網站中...");
                        Stream stream = new MemoryStream(await page.ScreenshotAsync());
                        sentMessage = await botClient.SendPhotoAsync(
                        chatId: chatId,
                        photo: stream,
                        parseMode: ParseMode.Html,
                        cancellationToken: cancellationToken);
                        return;
                    }
                    #endregion

                    #region Yahoo

                    if (messageText.Contains("/yahoo"))
                    {
                        if (text.Count != 2) return;

                        Console.WriteLine($"讀取網站中...");
                        await page.GotoAsync($"https://finance.yahoo.com/quote/{StockNumber}.TW/key-statistics?p={StockNumber}.TW",
                            new PageGotoOptions { WaitUntil = WaitUntilState.Load }).WaitAsync(new TimeSpan(0, 0, 30));
                        Console.WriteLine($"擷取網站中...");
                        var temp = await page.TextContentAsync("//div[@class= 'Pstart(20px) smartphone_Pstart(0px)'] >> //div[@class= 'Pos(r) Mt(10px)']");
                        Stream stream = new MemoryStream(await page.ScreenshotAsync());
                        sentMessage = await botClient.SendPhotoAsync(
                        chatId: chatId,
                        photo: stream,
                        parseMode: ParseMode.Html,
                        cancellationToken: cancellationToken);
                        return;
                    }
                    #endregion

                    #region TradingView

                    if (messageText.Contains("/chart"))
                    {
                        if (text.Count != 2) return;

                        Console.WriteLine($"讀取網站中...");
                        await page.GotoAsync($"https://tradingview.com/chart/?symbol=TWSE%3A{StockNumber}",
                            new PageGotoOptions { WaitUntil = WaitUntilState.Load }).WaitAsync(new TimeSpan(0, 1, 0));
                        Stream stream = new MemoryStream(await page.Locator("//table[@class= 'chart-markup-table']").ScreenshotAsync());
                        Console.WriteLine($"擷取網站中...");
                        sentMessage = await botClient.SendPhotoAsync(
                        chatId: chatId,
                        photo: stream,
                        parseMode: ParseMode.Html,
                        cancellationToken: cancellationToken);
                        return;
                    }

                    if (messageText.Contains("/range"))
                    {
                        if (text.Count != 3) return;

                        Console.WriteLine($"讀取網站中...");
                        await page.GotoAsync($"https://tradingview.com/chart/?symbol=TWSE%3A{StockNumber}",
                            new PageGotoOptions { WaitUntil = WaitUntilState.Load }).WaitAsync(new TimeSpan(0, 1, 0));
                        string range = null;
                        if (text.Count == 3)
                        {
                            switch (text[2])
                            {
                                case "1d":
                                    range = "1D";
                                    break;
                                case "5d":
                                    range = "5D";
                                    break;
                                case "1m":
                                    range = "1M";
                                    break;
                                case "3m":
                                    range = "3M";
                                    break;
                                case "6m":
                                    range = "6M";
                                    break;
                                case "ytd":
                                    range = "YTD";
                                    break;
                                case "1y":
                                    range = "12M";
                                    break;
                                case "5y":
                                    range = "60M";
                                    break;
                                case "all":
                                    range = "ALL";
                                    break;
                                default:
                                    range = "YTD";
                                    break;
                            }
                            await page.Locator($"//button[@value = '{range}']").ClickAsync();
                            await page.WaitForSelectorAsync("//table[@class= 'chart-markup-table']", new PageWaitForSelectorOptions
                            {
                                Timeout = 10000
                            });
                            Stream stream = new MemoryStream(await page.Locator("//table[@class= 'chart-markup-table']").ScreenshotAsync());
                            sentMessage = await botClient.SendPhotoAsync(
                            chatId: chatId,
                            photo: stream,
                            parseMode: ParseMode.Html,
                            cancellationToken: cancellationToken);
                        }
                        else
                        {
                            sentMessage = await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "找不到此代號，請重新輸入",
                            cancellationToken: cancellationToken);
                        }
                        return;
                    }
                    #endregion

                    #region 鉅亨網

                    //K線
                    if (messageText.Contains("/k"))
                    {
                        Console.WriteLine($"讀取網站中...");
                        await page.GotoAsync($"https://www.cnyes.com/twstock/{StockNumber}",
                            new PageGotoOptions { WaitUntil = WaitUntilState.Load }).WaitAsync(new TimeSpan(0, 1, 0));
                        Console.WriteLine($"擷取網站中...");
                        var stockName = await page.TextContentAsync("//h2[@class= 'jsx-3098318342']");
                        string range = "日K";
                        if (text.Count == 2)
                        {
                            await page.GetByRole(AriaRole.Button, new() { Name = range }).ClickAsync();
                            await page.WaitForTimeoutAsync(1000);

                            //圖表
                            Stream stream = new MemoryStream(await page.Locator("//div[@class= 'jsx-3625047685 tradingview-chart']").ScreenshotAsync());
                            sentMessage = await botClient.SendPhotoAsync(
                                caption: $"{stockName}：{range}線圖　💹",
                                chatId: chatId,
                                photo: stream,
                                parseMode: ParseMode.Html,
                                cancellationToken: cancellationToken);
                        }
                        else if (text.Count == 3)
                        {
                            switch (text[2].ToLower())
                            {
                                case "h":
                                    range = "分時";
                                    break;
                                case "d":
                                    range = "日K";
                                    break;
                                case "w":
                                    range = "週K";
                                    break;
                                case "m":
                                    range = "月K";
                                    break;
                                case "5m":
                                    range = "5分";
                                    break;
                                case "10m":
                                    range = "10分";
                                    break;
                                case "15m":
                                    range = "15分";
                                    break;
                                case "30m":
                                    range = "30分";
                                    break;
                                case "60m":
                                    range = "60分";
                                    break;
                                default:
                                    sentMessage = await botClient.SendTextMessageAsync(
                                        chatId: chatId,
                                        text: "指令錯誤請重新輸入",
                                        cancellationToken: cancellationToken);
                                    break;
                            }

                            await page.GetByRole(AriaRole.Button, new()
                            {
                                Name = range,
                                Exact = true,
                            }).ClickAsync();
                            await page.WaitForTimeoutAsync(1000);
                            //圖表
                            Stream stream = new MemoryStream(await page.Locator("//div[@class= 'jsx-3625047685 tradingview-chart']").ScreenshotAsync());
                            sentMessage = await botClient.SendPhotoAsync(
                                caption: $"{stockName}：{range}線圖　💹",
                                chatId: chatId,
                                photo: stream,
                                parseMode: ParseMode.Html,
                                cancellationToken: cancellationToken);
                        }
                        else
                        {
                            sentMessage = await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "指令錯誤請重新輸入",
                            cancellationToken: cancellationToken);
                        }
                        return;
                    }
                    //詳細報價
                    if (messageText.Contains("/v"))
                    {
                        if (text.Count != 2) return;

                        Console.WriteLine($"讀取網站中...");
                        await page.GotoAsync($"https://www.cnyes.com/twstock/{StockNumber}",
                            new PageGotoOptions { WaitUntil = WaitUntilState.Load }).WaitAsync(new TimeSpan(0, 1, 0));
                        Console.WriteLine($"擷取網站中...");
                        await page.GetByRole(AriaRole.Button, new() { Name = "日K" }).ClickAsync();
                        await page.WaitForTimeoutAsync(1000);
                        //詳細報價
                        var stockPrice = await page.TextContentAsync("//h3[@class= 'jsx-3098318342 rise']");
                        var returnStockUD = await page.QuerySelectorAllAsync("//div[@class= 'jsx-3098318342 first-row'] >> //span[@class= 'jsx-3098318342']");
                        var StockUD = new string[] { await returnStockUD[0].TextContentAsync(), await returnStockUD[1].TextContentAsync() };
                        var returnText = await page.QuerySelectorAllAsync("//li[@class= 'jsx-1282029765'] >> //span[@class= 'jsx-1282029765 value']");
                        //選擇輸出欄位
                        var output = new int[] { 0, 1, 4, 9 };

                        StringBuilder message = new StringBuilder();
                        int line = 0;

                        message.Append(@$"<code>收盤價：{stockPrice}</code>");
                        message.AppendLine();
                        message.Append(@$"<code>漲跌幅：{StockUD[0]}</code>");
                        message.AppendLine();
                        message.Append(@$"<code>漲跌%：{StockUD[1]}</code>");
                        message.AppendLine();

                        foreach (var i in output)
                        {
                            line++;
                            message.Append(@$"<code>{InfoDic[i]}：{await returnText[i].TextContentAsync()}</code>");
                            message.AppendLine();
                        }
                        //圖表
                        Stream stream = new MemoryStream(
                            await page.Locator("//div[@class= 'jsx-3625047685 tradingview-chart']").ScreenshotAsync());
                        sentMessage = await botClient.SendPhotoAsync(
                            caption: message.ToString(),
                            chatId: chatId,
                            photo: stream,
                            parseMode: ParseMode.Html,
                            cancellationToken: cancellationToken);
                        return;
                    }
                    //績效
                    if (messageText.Contains("/p"))
                    {
                        if (text.Count != 2) return;

                        Console.WriteLine($"讀取網站中...");
                        await page.GotoAsync($"https://www.cnyes.com/twstock/{StockNumber}",
                            new PageGotoOptions { WaitUntil = WaitUntilState.Load }).WaitAsync(new TimeSpan(0, 1, 0));
                        Console.WriteLine($"擷取網站中...");
                        var stockName = await page.TextContentAsync("//h2[@class= 'jsx-3098318342']");
                        //股價
                        var stream = new MemoryStream(await page.Locator("//table[@class= 'jsx-960859598 flex']").ScreenshotAsync());
                        sentMessage = await botClient.SendPhotoAsync(
                            caption: $"{stockName} 績效表現　✨",
                            chatId: chatId,
                            photo: stream,
                            parseMode: ParseMode.Html,
                            cancellationToken: cancellationToken);
                        return;
                    }
                    //新聞
                    if (messageText.Contains("/n"))
                    {
                        if (text.Count != 2) return;

                        Console.WriteLine($"讀取網站中...");
                        await page.GotoAsync($"https://www.cnyes.com/twstock/{StockNumber}",
                            new PageGotoOptions { WaitUntil = WaitUntilState.Load }).WaitAsync(new TimeSpan(0, 1, 0));
                        Console.WriteLine($"擷取網站中...");

                        var stockName = await page.TextContentAsync("//h2[@class= 'jsx-3098318342']");
                        var returnTitle = await page.QuerySelectorAllAsync("//h3[@class= 'jsx-2831776980']");
                        var returnUrl = await page.QuerySelectorAllAsync("//a[@class= 'jsx-2831776980 container shadow']");

                        var InlineList = new List<IEnumerable<InlineKeyboardButton>>();
                        for (int i = 0; i < 5; i++)
                        {
                            InlineList.Add(new[] { InlineKeyboardButton.WithUrl(await returnTitle[i].TextContentAsync(), await returnUrl[i].GetAttributeAsync("href")) });
                        }

                        InlineKeyboardMarkup inlineKeyboard = new(InlineList);
                        var s = inlineKeyboard.InlineKeyboard;
                        sentMessage = await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: @$"{stockName}：即時新聞⚡️",
                            replyMarkup: inlineKeyboard,
                            cancellationToken: cancellationToken
                            );

                        return;
                    }
                    #endregion
                }
                catch (ApiRequestException ex)
                {
                    Console.WriteLine(ex.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
            {
                var ErrorMessage = exception switch
                {
                    ApiRequestException apiRequestException
                        => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                    _ => exception.ToString()
                };
                Console.WriteLine(ErrorMessage);
                cts.Cancel();
                botClient.CloseAsync(cancellationToken);
                return Task.CompletedTask;
            }

        }
    }
}
