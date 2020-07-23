﻿using Discord;
using Discord.Commands;
using FFXIVWeather;
using FFXIVWeather.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Prima.Unstable.Modules
{
    /// <summary>
    /// Includes commands that haven't been thoroughly tested (wait, none of them have) and shouldn't hit Stable.
    /// </summary>
    [Name("Unstable")]
    public class UnstableModule : ModuleBase<SocketCommandContext>
    {
        public FFXIVWeatherService Weather { get; set; }
        public HttpClient Http { get; set; }

        [Command("weather")]
        public async Task WeatherAsync([Remainder]string zone)
        {
            IList<(Weather, DateTime)> forecast;
            try
            {
                forecast = Weather.GetForecast(zone, count: 10);
            }
            catch (ArgumentException)
            {
                await ReplyAsync("The specified zone could not be found.");
                return;
            }

            var (currentWeather, currentWeatherStartTime) = forecast[0];

            var formattedForecast = $"**Current:** {currentWeather} (Began at {currentWeatherStartTime.ToShortTimeString()})";
            foreach (var (weather, startTime) in forecast.Skip(1))
            {
                formattedForecast += $"\n{startTime.ToShortTimeString()}: {weather}";
            }

            var embed = new EmbedBuilder()
                .WithAuthor(new EmbedAuthorBuilder()
                    .WithIconUrl(await GetWeatherIconUrl(currentWeather.Id))
                    .WithName($"Current weather for {Util.JadenCase(zone)}"))
                .WithTitle($"Next weather starts in {(new DateTime() - forecast[1].Item2).TotalMinutes} minutes.")
                .WithColor(Color.LightOrange)
                .WithDescription(formattedForecast)
                .Build();

            await ReplyAsync(embed: embed);
        }

        private async Task<string> GetWeatherIconUrl(int weatherId)
        {
            var data = JObject.Parse(await Http.GetStringAsync($"https://xivapi.com/Weather/weatherId"));
            var urlPartial = data["Icon"].ToObject<string>();
            return $"https://xivapi.com{urlPartial}";
        }
    }
}
