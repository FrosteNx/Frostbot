using AngleSharp.Dom;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Frostbot.commands
{
    internal class MusicCommands : BaseCommandModule
    {
        private List<LavalinkTrack> trackQueue = new List<LavalinkTrack>();

        [Command("play")]
        public async Task Play(CommandContext ctx, [RemainingText] string query)
        {
            var userVC = ctx.Member.VoiceState.Channel;
            var lavalinkInstance = ctx.Client.GetLavalink();

            if(ctx.Member.VoiceState == null || userVC == null)
            {
                await ctx.Channel.SendMessageAsync("You need to enter a VC");
                return;
            }

            if(!lavalinkInstance.ConnectedNodes.Any())
            {
                await ctx.Channel.SendMessageAsync("Connection is not established");
                return;
            }

            if(userVC.Type != ChannelType.Voice)
            {
                await ctx.Channel.SendMessageAsync("You need to enter a valid VC");
                return;
            }

            var node = lavalinkInstance.ConnectedNodes.Values.First();
            await node.ConnectAsync(userVC);

            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
            if(conn == null)
            {
                await ctx.Channel.SendMessageAsync("Connection failed");
                return;
            }

            var searchQuery = await node.Rest.GetTracksAsync(query);
            if(searchQuery.LoadResultType == LavalinkLoadResultType.NoMatches || searchQuery.LoadResultType == LavalinkLoadResultType.LoadFailed)
            {
                await ctx.Channel.SendMessageAsync($"Failed to find music with provided query: {query}");
                return;
            }

            var musicTrack = searchQuery.Tracks.First();

            await conn.PlayAsync(musicTrack);

            string musicDescription = $"Now playing: {musicTrack.Title} \n" +
                                      $"Author: {musicTrack.Author} \n" +
                                      $"Length: {musicTrack.Length} \n" +
                                      $"URL: {musicTrack.Uri}";

            var nowPlayingEmbed = new DiscordEmbedBuilder()
            {
                Color = DiscordColor.Purple,
                Title = $"Succesfully joined channel {userVC.Name} and playing music",
                Description = musicDescription
            };

            await ctx.Channel.SendMessageAsync(embed : nowPlayingEmbed);

            conn.PlaybackFinished += async (s, e) =>
            {
                await conn.DisconnectAsync();
            };
        }

        [Command("pause")]
        public async Task Pause(CommandContext ctx, [RemainingText] string query)
        {
            var userVC = ctx.Member.VoiceState.Channel;
            var lavalinkInstance = ctx.Client.GetLavalink();

            if (ctx.Member.VoiceState == null || userVC == null)
            {
                await ctx.Channel.SendMessageAsync("You need to enter a VC");
                return;
            }

            if (!lavalinkInstance.ConnectedNodes.Any())
            {
                await ctx.Channel.SendMessageAsync("Connection is not established");
                return;
            }

            if (userVC.Type != ChannelType.Voice)
            {
                await ctx.Channel.SendMessageAsync("You need to enter a valid VC");
                return;
            }

            var node = lavalinkInstance.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if(conn == null) 
            {
                await ctx.Channel.SendMessageAsync("Connection failed");
                return;
            }

            if(conn.CurrentState.CurrentTrack == null)
            {
                await ctx.Channel.SendMessageAsync("No track is playing now");
                return;
            }

            await conn.PauseAsync();

            var pausedEmbed = new DiscordEmbedBuilder()
            {
                Color = DiscordColor.Purple,
                Title = "Track paused"
            };

            await ctx.Channel.SendMessageAsync(embed : pausedEmbed);
        }

        [Command("resume")]
        public async Task Resume(CommandContext ctx, [RemainingText] string query)
        {
            var userVC = ctx.Member.VoiceState.Channel;
            var lavalinkInstance = ctx.Client.GetLavalink();

            if (ctx.Member.VoiceState == null || userVC == null)
            {
                await ctx.Channel.SendMessageAsync("You need to enter a VC");
                return;
            }

            if (!lavalinkInstance.ConnectedNodes.Any())
            {
                await ctx.Channel.SendMessageAsync("Connection is not established");
                return;
            }

            if (userVC.Type != ChannelType.Voice)
            {
                await ctx.Channel.SendMessageAsync("You need to enter a valid VC");
                return;
            }

            var node = lavalinkInstance.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (conn == null)
            {
                await ctx.Channel.SendMessageAsync("Connection failed");
                return;
            }

            if (conn.CurrentState.CurrentTrack == null)
            {
                await ctx.Channel.SendMessageAsync("No track is playing now");
                return;
            }

            await conn.ResumeAsync();

            var resumedEmbed = new DiscordEmbedBuilder()
            {
                Color = DiscordColor.Purple,
                Title = "Track resumed"
            };

            await ctx.Channel.SendMessageAsync(embed: resumedEmbed);
        }

        [Command("stop")]
        public async Task Stop(CommandContext ctx, [RemainingText] string query)
        {
            var userVC = ctx.Member.VoiceState.Channel;
            var lavalinkInstance = ctx.Client.GetLavalink();

            if (ctx.Member.VoiceState == null || userVC == null)
            {
                await ctx.Channel.SendMessageAsync("You need to enter a VC");
                return;
            }

            if (!lavalinkInstance.ConnectedNodes.Any())
            {
                await ctx.Channel.SendMessageAsync("Connection is not established");
                return;
            }

            if (userVC.Type != ChannelType.Voice)
            {
                await ctx.Channel.SendMessageAsync("You need to enter a valid VC");
                return;
            }

            var node = lavalinkInstance.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (conn == null)
            {
                await ctx.Channel.SendMessageAsync("Connection failed");
                return;
            }

            if (conn.CurrentState.CurrentTrack == null)
            {
                await ctx.Channel.SendMessageAsync("No track is playing now");
                return;
            }

            await conn.StopAsync();
            await conn.DisconnectAsync();

            var stoppedEmbed = new DiscordEmbedBuilder()
            {
                Color = DiscordColor.Purple,
                Title = "Track stopped"
            };

            await ctx.Channel.SendMessageAsync(embed: stoppedEmbed);
        }

        [Command("queue")]
        public async Task Queue(CommandContext ctx, [RemainingText] string query)
        {
            var lavalinkInstance = ctx.Client.GetLavalink();
            var node = lavalinkInstance.ConnectedNodes.Values.First();

            var searchQuery = await node.Rest.GetTracksAsync(query);

            if(searchQuery.LoadResultType != LavalinkLoadResultType.NoMatches && searchQuery.LoadResultType != LavalinkLoadResultType.LoadFailed)
            {
                var musicTrack = searchQuery.Tracks.First();
                trackQueue.Add(musicTrack);
                await ctx.Channel.SendMessageAsync($"Added {musicTrack.Title} to the queue.");
            }
            else
                await ctx.Channel.SendMessageAsync($"Failed to find track with provided query: {query}");
        }

        [Command("viewqueue")]
        public async Task ViewQueue(CommandContext ctx)
        {
            if(trackQueue.Any())
            {
                var queueDescription = string.Join("\n", trackQueue.Select((track, index) => $"{index + 1}. {track.Title} - {track.Author}"));
                await ctx.Channel.SendMessageAsync($"Current queue:\n{queueDescription}");
            }
            else
            {
                await ctx.Channel.SendMessageAsync("Queue is empty");
            }
        }
    }
}
