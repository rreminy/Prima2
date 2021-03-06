﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Prima.Models;
using Prima.Services;
using Serilog;

namespace Prima.Stable.Handlers
{
    public static class ReactionReceived
    {
        public static async Task HandlerAdd(DbService db, Cacheable<IUserMessage, ulong> _, ISocketMessageChannel ichannel, SocketReaction reaction)
        {
            if (ichannel is SocketGuildChannel channel)
            {
                var guild = channel.Guild;
                var member = guild.GetUser(reaction.UserId);
                var disConfig = db.Guilds.FirstOrDefault(g => g.Id == guild.Id);
                if (disConfig == null)
                {
                    return;
                }
                if ((ichannel.Id == 551584585432039434 || ichannel.Id == 590757405927669769 || ichannel.Id == 765748243367591936) && reaction.Emote is Emote emote && disConfig.RoleEmotes.TryGetValue(emote.Id.ToString(), out var roleIdString))
                {
                    var roleId = ulong.Parse(roleIdString);
                    var role = member.Guild.GetRole(roleId);
                    await member.AddRoleAsync(role);
                    Log.Information("Role {Role} was added to {DiscordUser}", role.Name, member.ToString());
                }
                else if (guild.Id == 550702475112480769 && (ichannel.Id == 552643167808258060 || ichannel.Id == 768886934084648960) && reaction.Emote.Name == "✅")
                {
                    await member.SendMessageAsync($"You have begun the verification process. Your **Discord account ID** is `{member.Id}`.\n"
                                                  + "Please add this somewhere in your FFXIV Lodestone Character Profile.\n"
                                                  + "You can edit your account description here: https://na.finalfantasyxiv.com/lodestone/my/setting/profile/\n\n"
                                                  + $"After you have put your Discord account ID in your Lodestone profile, please use `{db.Config.Prefix}verify` to get your clear role.\n"
                                                  + "The Lodestone may not immediately update following updates to your achievements, so please wait a few hours and try again if this is the case.");
                }
            }
        }

        public static async Task HandlerRemove(DbService db, Cacheable<IUserMessage, ulong> _, ISocketMessageChannel ichannel, SocketReaction reaction)
        {
            if (ichannel is SocketGuildChannel channel)
            {
                var guild = channel.Guild;
                var member = guild.GetUser(reaction.UserId);
                DiscordGuildConfiguration disConfig;
                try
                {
                    disConfig = db.Guilds.Single(g => g.Id == guild.Id);
                }
                catch (InvalidOperationException)
                {
                    return;
                }
                if ((ichannel.Id == 551584585432039434 || ichannel.Id == 590757405927669769 || ichannel.Id == 765748243367591936) && reaction.Emote is Emote emote && disConfig.RoleEmotes.TryGetValue(emote.Id.ToString(), out var roleIdString))
                {
                    var roleId = ulong.Parse(roleIdString);
                    var role = member.Guild.GetRole(roleId);
                    await member.RemoveRoleAsync(role);
                    Log.Information("Role {Role} was removed from {DiscordUser}", role.Name, member.ToString());
                }
            }
        }
    }
}