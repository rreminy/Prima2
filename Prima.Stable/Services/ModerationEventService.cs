﻿using Discord;
using Discord.Net;
using Discord.WebSocket;
using Prima.Models;
using Prima.Services;
using Serilog;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Color = Discord.Color;
using ImageFormat = System.Drawing.Imaging.ImageFormat;

namespace Prima.Stable.Services
{
    public class ModerationEventService
    {
        private readonly DbService _db;
        private readonly DiscordSocketClient _client;
        private readonly WebClient _wc;

        public string LastCaughtRegex { get; private set; }

        public ModerationEventService(DbService db, DiscordSocketClient client, WebClient wc)
        {
            _db = db;
            _client = client;
            _wc = wc;

            LastCaughtRegex = string.Empty;
        }

        public async Task MessageDeleted(Cacheable<IMessage, ulong> cmessage, ISocketMessageChannel ichannel)
        {
            if (!(ichannel is SocketGuildChannel channel) || _db.Guilds.All(g => g.Id != channel.Guild.Id)) return;

            var guild = channel.Guild;

            var config = _db.Guilds.Single(g => g.Id == guild.Id);

            var deletedMessageChannel = guild.GetChannel(config.DeletedMessageChannel) as SocketTextChannel ?? throw new NullReferenceException();
            var deletedCommandChannel = guild.GetChannel(config.DeletedCommandChannel) as SocketTextChannel ?? throw new NullReferenceException();

            CachedMessage cachedMessage;
            var imessage = await cmessage.GetOrDownloadAsync();
            if (imessage == null)
            {
                cachedMessage = _db.CachedMessages.FirstOrDefault(m => m.MessageId == cmessage.Id);
                if (cachedMessage == null)
                {
                    //await deletedMessageChannel.SendMessageAsync(
                    //    "A message was deleted without being cached first! This probably happened in the `welcome` channel.");
                    Log.Warning("Message deleted and not cached! This probably happened in #welcome.");
                    return;
                }
            }
            else
            {
                cachedMessage = new CachedMessage
                {
                    AuthorId = imessage.Author.Id,
                    ChannelId = imessage.Channel.Id,
                    Content = imessage.Content,
                    MessageId = cmessage.Id,
                    UnixMs = imessage.Timestamp.ToUnixTimeMilliseconds(),
                };
            }

            var prefix = config.Prefix == ' ' ? _db.Config.Prefix : config.Prefix;

            // Get executor of the deletion.
            var auditLogs = await guild.GetAuditLogsAsync(10).FlattenAsync();
            var author = _client.GetUser(cachedMessage.AuthorId);
            IUser executor = author; // If no user is listed as the executor, the executor is the author of the message.
            try
            {
                var thisLog = auditLogs
                    .FirstOrDefault(log => log.Action == ActionType.MessageDeleted && DateTime.Now - log.CreatedAt < new TimeSpan(0, 5, 0));
                executor = thisLog?.User ?? executor; // See above.
            }
            catch (InvalidOperationException) { }

            // Build the embed.
            var messageEmbed = new EmbedBuilder()
                .WithTitle("#" + ichannel.Name)
                .WithColor(Color.Blue)
                .WithAuthor(author)
                .WithDescription(cachedMessage.Content)
                .WithFooter($"Deleted by {executor}", executor.GetAvatarUrl())
                .WithCurrentTimestamp()
                .Build();

            // Send the embed.
            if (author.Id == _client.CurrentUser.Id || cachedMessage.Content.StartsWith(prefix))
            {
                await deletedCommandChannel.SendMessageAsync(embed: messageEmbed);
            }
            else
            {
                await deletedMessageChannel.SendMessageAsync(embed: messageEmbed);
            }

            // TODO Attach attachments as well.
            /*var unsaved = string.Empty;
            foreach (var attachment in message.Attachments)
            {
                try
                {
                    await deletedMessageChannel.SendFileAsync(Path.Combine(_db.Config.TempDir, attachment.Filename), attachment.Filename);
                }
                catch (HttpException)
                {
                    unsaved += $"\n{attachment.Url}";
                }
            }
            if (!string.IsNullOrEmpty(unsaved))
            {
                await deletedMessageChannel.SendMessageAsync(Properties.Resources.UnsavedMessageAttachmentsWarning + unsaved);
            }*/

            // Copy reactions and send those, too.
            /*
            if (message.Reactions.Count > 0)
            {
                string userString = string.Empty;
                foreach (var reactionEntry in message.Reactions)
                {
                    var emote = reactionEntry.Key;
                    userString += $"\nUsers who reacted with {emote}:";
                    IEnumerable<IUser> users = await message.GetReactionUsersAsync(emote, int.MaxValue).FlattenAsync();
                    foreach (IUser user in users)
                    {
                        userString += "\n" + user.Mention;
                    }
                }
                await deletedMessageChannel.SendMessageAsync(userString);
            }*/
        }

        public async Task MessageRecieved(SocketMessage rawMessage)
        {
            if (rawMessage == null)
            {
                throw new ArgumentNullException(nameof(rawMessage));
            }

            SaveAttachments(rawMessage);

            if (!(rawMessage.Channel is SocketGuildChannel channel))
                return;

            if (_db.Guilds.All(g => g.Id != channel.Guild.Id)) return;
            var guildConfig = _db.Guilds.Single(g => g.Id == channel.Guild.Id);

            // Keep the welcome channel clean.
            if (rawMessage.Channel.Id == guildConfig.WelcomeChannel)
            {
                var guild = channel.Guild;
                var prefix = guildConfig.Prefix == ' ' ? _db.Config.Prefix : guildConfig.Prefix;
                if (!guild.GetUser(rawMessage.Author.Id).GetPermissions(channel).ManageMessages)
                {
                    if (!rawMessage.Content.StartsWith($"{prefix}i") && !rawMessage.Content.ToLower().StartsWith("i") && !rawMessage.Content.StartsWith($"{prefix}agree") && !rawMessage.Content.StartsWith($"agree"))
                    {
                        try
                        {
                            await rawMessage.DeleteAsync();
                        }
                        catch (HttpException) { }
                    }
                    else
                    {
                        try
                        {
                            await Task.Delay(10000);
                            await rawMessage.DeleteAsync();
                        }
                        catch (HttpException) { }
                    }
                }
            }

            if (!rawMessage.Content.StartsWith("~report"))
            {
                await ProcessAttachments(rawMessage, channel);
            }
            await CheckTextBlacklist(rawMessage, guildConfig);
        }

        /// <summary>
        /// Check a message against the text blacklist.
        /// </summary>
        public async Task CheckTextBlacklist(SocketMessage rawMessage, DiscordGuildConfiguration guildConfig)
        {
            foreach (var regexString in guildConfig.TextBlacklist)
            {
                var match = Regex.Match(rawMessage.Content, regexString);
                if (match.Success)
                {
                    LastCaughtRegex = regexString;
                    await rawMessage.DeleteAsync();
                }
            }
        }

        /// <summary>
        /// Save attachments to a local directory. Remember to clear out this folder periodically.
        /// </summary>
        private void SaveAttachments(SocketMessage rawMessage)
        {
            if (!rawMessage.Attachments.Any()) return;
            foreach (var a in rawMessage.Attachments)
            {
                _wc.DownloadFile(new Uri(a.Url), Path.Combine(_db.Config.TempDir, a.Filename));
                Log.Information("Saved attachment {Filename}", Path.Combine(_db.Config.TempDir, a.Filename));
            }
        }

        /// <summary>
        /// Convert attachments that don't render automatically to formats that do.
        /// </summary>
        private async Task ProcessAttachments(SocketMessage rawMessage, SocketGuildChannel guildChannel)
        {
            if (!rawMessage.Attachments.Any()) return;

            foreach (var attachment in rawMessage.Attachments)
            {
                var justFileName = attachment.Filename.Substring(0, attachment.Filename.LastIndexOf("."));
                if (attachment.Filename.ToLower().EndsWith(".bmp") || attachment.Filename.ToLower().EndsWith(".dib"))
                {
                    try
                    {
                        var timer = new Stopwatch();
                        using var bitmap = new Bitmap(Path.Combine(_db.Config.TempDir, attachment.Filename));
                        bitmap.Save(Path.Combine(_db.Config.TempDir, justFileName + ".png"), ImageFormat.Png);
                        timer.Stop();
                        Log.Information("Processed BMP from {DiscordName}, ({Time}ms)!", $"{rawMessage.Author.Username}#{rawMessage.Author.Discriminator}", timer.ElapsedMilliseconds);
                        await (guildChannel as ITextChannel).SendFileAsync(Path.Combine(_db.Config.TempDir, justFileName + ".png"), $"{rawMessage.Author.Mention}: Your file has been automatically converted from BMP/DIB to PNG (BMP files don't render automatically).");
                    }
                    catch (FileNotFoundException)
                    {
                        Log.Error("Could not find file {Filename}", Path.Combine(_db.Config.TempDir, attachment.Filename));
                    }
                }
            }
        }
    }
}
