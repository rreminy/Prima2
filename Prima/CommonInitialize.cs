﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Prima.Services;
using Serilog;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Discord.Net;

namespace Prima
{
    public static class CommonInitialize
    {
        public static IServiceCollection Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console()
                .WriteTo.SQLite(Environment.OSVersion.Platform == PlatformID.Win32NT
                    ? "Log.db" // Only use Windows for testing.
                    : Path.Combine(Environment.GetEnvironmentVariable("HOME"), "log/Log.db"))
                .CreateLogger();

            var disConfig = new DiscordSocketConfig
            {
                AlwaysDownloadUsers = true,
                LargeThreshold = 250,
                MessageCacheSize = 10000,
            };

            return ConfigurePartialServiceCollection(disConfig);
        }

        public static async Task ConfigureServicesAsync(ServiceProvider services)
        {
            var client = services.GetRequiredService<DiscordSocketClient>();
                
            client.Log += LogAsync;
            services.GetRequiredService<CommandService>().Log += LogAsync;

            await client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("PRIMA_BOT_TOKEN"));
            await client.StartAsync();

            await services.GetRequiredService<CommandHandlingService>().InitializeAsync();
        }

        private static IServiceCollection ConfigurePartialServiceCollection(DiscordSocketConfig disConfig)
        {
            return new ServiceCollection()
                .AddSingleton(new DiscordSocketClient(disConfig))
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandlingService>()
                .AddSingleton<DiagnosticService>()
                .AddSingleton<HttpClient>()
                .AddSingleton<DbService>()
                .AddSingleton<FFXIVSheetService>()
                .AddSingleton<PasswordGenerator>();
                //.AddSingleton(new HttpServer(Log.Information))
        }

        private static Task LogAsync(LogMessage message)
        {
            switch (message.Severity)
            {
                case LogSeverity.Critical:
                    Log.Error(message.ToString());
                    break;
                case LogSeverity.Error:
                    Log.Error(message.ToString());
                    break;
                case LogSeverity.Warning:
                    Log.Warning(message.ToString());
                    break;
                case LogSeverity.Info:
                    Log.Information(message.ToString());
                    break;
                case LogSeverity.Verbose:
                    Log.Verbose(message.ToString());
                    break;
                case LogSeverity.Debug:
                    Log.Debug(message.ToString());
                    break;
            }
            return Task.CompletedTask;
        }
    }
}
