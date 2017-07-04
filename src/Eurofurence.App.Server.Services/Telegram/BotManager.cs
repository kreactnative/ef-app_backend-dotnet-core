﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Eurofurence.App.Domain.Model.Abstractions;
using Eurofurence.App.Domain.Model.PushNotifications;
using Eurofurence.App.Server.Services.Abstraction.Telegram;
using Eurofurence.App.Server.Services.Abstractions.Dealers;
using Eurofurence.App.Server.Services.Abstractions.Events;
using Eurofurence.App.Server.Services.Abstractions.Images;
using Eurofurence.App.Server.Services.Abstractions.Security;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputMessageContents;
using Eurofurence.App.Server.Services.Abstractions.Telegram;

// ReSharper disable CoVariantArrayConversion

namespace Eurofurence.App.Server.Services.Telegram
{
    public class BotManager
    {
        private readonly ITelegramUserManager _telegramUserManager;
        private readonly IDealerService _dealerService;
        private readonly IEventService _eventService;
        private readonly IEventConferenceRoomService _eventConferenceRoomService;
        private readonly IRegSysAlternativePinAuthenticationProvider _regSysAlternativePinAuthenticationProvider;
        private readonly IEntityRepository<PushNotificationChannelRecord> _pushNotificationChannelRepository;
        private readonly TelegramBotClient _botClient;
        private readonly ConversationManager _conversationManager;

        internal class MiniProxy : IWebProxy
        {
            private readonly string _uri;

            public MiniProxy(string uri)
            {
                _uri = uri;
            }
            public Uri GetProxy(Uri destination)
            {
                return new Uri(_uri);
            }

            public bool IsBypassed(Uri host)
            {
                return false;
            }

            public ICredentials Credentials { get; set; }
        }

        public BotManager(
            TelegramConfiguration telegramConfiguration,
            ITelegramUserManager telegramUserManager,
            IDealerService dealerService,
            IEventService eventService,
            IEventConferenceRoomService eventConferenceRoomService,
            IRegSysAlternativePinAuthenticationProvider regSysAlternativePinAuthenticationProvider,
            IEntityRepository<PushNotificationChannelRecord> pushNotificationChannelRepository
            )
        {
            _telegramUserManager = telegramUserManager;
            _dealerService = dealerService;
            _eventService = eventService;
            _eventConferenceRoomService = eventConferenceRoomService;
            _regSysAlternativePinAuthenticationProvider = regSysAlternativePinAuthenticationProvider;
            _pushNotificationChannelRepository = pushNotificationChannelRepository;

            _botClient =
                string.IsNullOrEmpty(telegramConfiguration.Proxy)
                    ? new TelegramBotClient(telegramConfiguration.AccessToken)
                    : new TelegramBotClient(telegramConfiguration.AccessToken,
                        new MiniProxy(telegramConfiguration.Proxy));

            _conversationManager = new ConversationManager(
                _botClient,
                (chatId) => new AdminConversation(
                    _telegramUserManager, 
                    _regSysAlternativePinAuthenticationProvider, 
                    _pushNotificationChannelRepository)
                );

            _botClient.OnMessage += BotClientOnOnMessage;
            _botClient.OnCallbackQuery += BotClientOnOnCallbackQuery;

            _botClient.OnInlineQuery += BotClientOnOnInlineQuery;
        }

        private async Task<InlineQueryResult[]> QueryEvents(string query)
        {
            if (query.Length < 3) return new InlineQueryResult[0];

            var events =
                (await _eventService.FindAllAsync(a => a.IsDeleted == 0 && a.Title.ToLower().Contains(query.ToLower())))
                .OrderBy(a => a.StartDateTimeUtc)
                .Take(10)
                .ToList();

            if (events.Count == 0) return new InlineQueryResult[0];

            var eventConferenceRooms = await _eventConferenceRoomService.FindAllAsync();

            return events.Select(e =>
                {
                    e.ConferenceRoom = eventConferenceRooms.Single(r => r.Id == e.ConferenceRoomId);

                    var messageBuilder = new StringBuilder();
                    messageBuilder.Append($"*{e.Title}*");

                    if (!string.IsNullOrEmpty(e.SubTitle))
                        messageBuilder.Append($" - ({e.SubTitle})");

                    messageBuilder.Append(
                        $"\n{e.StartDateTimeUtc.DayOfWeek}, {e.StartTime} to {e.EndTime} in {e.ConferenceRoom.Name}");

                    if (!string.IsNullOrEmpty(e.Description))
                    {
                        var desc = e.Description;
                        if (desc.Length > 500) desc = desc.Substring(0, 500) + "...";
                        messageBuilder.Append($"\n\n_{desc}_");
                    }

                    messageBuilder.Append("\n\n[Read more...](https://app.eurofurence.org)");

                    return new InlineQueryResultArticle()
                    {
                        Id = e.Id.ToString(),
                        InputMessageContent = new InputTextMessageContent()
                        {
                            MessageText = messageBuilder.ToString(),
                            ParseMode = ParseMode.Markdown
                        },
                        Title = e.Title + (string.IsNullOrEmpty(e.SubTitle) ? "" : $" ({e.SubTitle})"),
                        Description =
                            $"{e.StartDateTimeUtc.DayOfWeek}, {e.StartDateTimeUtc.Day}.{e.StartDateTimeUtc.Month} - {e.StartTime} until {e.EndTime}"
                    };
                })
                .ToArray();
        }

        private async Task<InlineQueryResult[]> QueryDealers(string query)
        {
            if (query.Length < 3) return new InlineQueryResult[0];

            var dealers =
                (await _dealerService.FindAllAsync(a => a.IsDeleted == 0 && (
                    a.DisplayName.ToLower().Contains(query.ToLower()) || a.AttendeeNickname.ToLower().Contains(query.ToLower())
                    )))
                .Take(5)
                .ToList();

            if (dealers.Count == 0) return new InlineQueryResult[0];

            return dealers.Select(e =>
                {
                    var messageBuilder = new StringBuilder();
                    messageBuilder.Append($"*{e.AttendeeNickname} {e.DisplayName}*");

                    messageBuilder.Append("\n\n[Read more...](https://app.eurofurence.org)");

                    return new InlineQueryResultPhoto()
                    {
                        Id = e.Id.ToString(),
                        Url = "https://app.eurofurence.org/images/qrcode_getAndroidApp.png",
                        ThumbUrl = "https://app.eurofurence.org/images/qrcode_getAndroidApp.png",
                        Title = e.AttendeeNickname,
                        Description = "..."
                    };
                })
                .ToArray();
        }


        private async void BotClientOnOnInlineQuery(object sender, InlineQueryEventArgs inlineQueryEventArgs)
        {
            try
            {
                var queryString = inlineQueryEventArgs.InlineQuery.Query;
                var queries = new[]
                {
                    QueryEvents(queryString),
                  //  QueryDealers(queryString)
                };

                Task.WaitAll(queries);
                var results = queries.SelectMany(task => task.Result).ToArray();

                if (results.Length == 0) return;

                await _botClient.AnswerInlineQueryAsync(
                    inlineQueryEventArgs.InlineQuery.Id,
                    results,
                    cacheTime: 0);
            }
            catch (Exception e)
            {
            }
        }

        public void Start()
        {
            _botClient.StartReceiving();
        }


        private Dictionary<int, DateTime> _answerredQueries = new Dictionary<int, DateTime>();

        private async void BotClientOnOnCallbackQuery(object sender, CallbackQueryEventArgs e)
        {
            try
            {
                await _botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id);

                lock (_answerredQueries)
                {
                    _answerredQueries.Where(a => DateTime.UtcNow.AddMinutes(-5) > a.Value)
                        .ToList()
                        .ForEach(a => _answerredQueries.Remove(a.Key));

                    if (e.CallbackQuery.Message.Date < DateTime.UtcNow.AddMinutes(-5))
                        return;

                    if (_answerredQueries.ContainsKey(e.CallbackQuery.Message.MessageId))
                        return;

                    _answerredQueries.Add(e.CallbackQuery.Message.MessageId, DateTime.UtcNow);
                }
                
                await _conversationManager[e.CallbackQuery.From.Id].OnCallbackQueryAsync(e);
            }
            catch (Exception exception)
            {
            }
        }

        private async void BotClientOnOnMessage(object sender, MessageEventArgs e)
        {
            try
            {
                await _conversationManager[e.Message.From.Id].OnMessageAsync(e);
            }
            catch (Exception exception)
            {
            }
        }
    }
}