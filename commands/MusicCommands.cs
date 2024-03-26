using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using DSharpPlus.Lavalink.EventArgs;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Frostbot.commands
{
    internal class MusicCommands : BaseCommandModule
    {
        private List<LavalinkTrack> trackQueue = new List<LavalinkTrack>();
        private Timer disconnectTimer;
        private int disconnectDelayInSeconds = 10;
        private bool shouldSkipTrack = false;

        [Command("play")]
        public async Task Play(CommandContext ctx, [RemainingText] string query)
        {
            if(disconnectTimer != null)
                disconnectTimer?.Dispose();

            await VoiceState(ctx);
            if(ctx.Member.VoiceState == null)
                return;

            var userVC = ctx.Member.VoiceState.Channel;
            var lavalinkInstance = ctx.Client.GetLavalink();

            await ChannelAndConnection(ctx, userVC, lavalinkInstance);
            if(userVC == null || !lavalinkInstance.ConnectedNodes.Any() || userVC.Type != ChannelType.Voice)
                return;

            var node = lavalinkInstance.ConnectedNodes.Values.First();
            await node.ConnectAsync(userVC);

            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
            if(conn == null)
            {
                await ctx.Channel.SendMessageAsync("Connection failed");
                return;
            }

            conn.PlaybackFinished += async (connection, args) => await LavalinkSocketOnTrackEnd(ctx, conn, args);

            if(trackQueue.Any())
            {
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
            else
            {
                var searchQuery = await node.Rest.GetTracksAsync(query);
                if(searchQuery.LoadResultType == LavalinkLoadResultType.NoMatches || searchQuery.LoadResultType == LavalinkLoadResultType.LoadFailed)
                {
                    await ctx.Channel.SendMessageAsync($"Failed to find music with provided query: {query}");
                    return;
                }

                var musicTrack = searchQuery.Tracks.First();

                await conn.PlayAsync(musicTrack);

                var nowPlayingEmbed = CreateNowPlayingEmbed(musicTrack);

                await ctx.Channel.SendMessageAsync(embed: nowPlayingEmbed);
            }
        }

        [Command("pause")]
        public async Task Pause(CommandContext ctx, [RemainingText] string query)
        {
            await VoiceState(ctx);
            if(ctx.Member.VoiceState == null)
                return;

            var userVC = ctx.Member.VoiceState.Channel;
            var lavalinkInstance = ctx.Client.GetLavalink();

            await ChannelAndConnection(ctx, userVC, lavalinkInstance);
            if(userVC == null || !lavalinkInstance.ConnectedNodes.Any() || userVC.Type != ChannelType.Voice)
                return;

            var node = lavalinkInstance.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            await Connection(ctx, conn);

            if(conn == null || conn.CurrentState.CurrentTrack == null)
                return;

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
            await VoiceState(ctx);
            if(ctx.Member.VoiceState == null)
                return;

            var userVC = ctx.Member.VoiceState.Channel;
            var lavalinkInstance = ctx.Client.GetLavalink();

            await ChannelAndConnection(ctx, userVC, lavalinkInstance);
            if(userVC == null || !lavalinkInstance.ConnectedNodes.Any() || userVC.Type != ChannelType.Voice)
                return;

            var node = lavalinkInstance.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            await Connection(ctx, conn);

            if(conn == null || conn.CurrentState.CurrentTrack == null)
                return;

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
            await VoiceState(ctx);
            if(ctx.Member.VoiceState == null)
                return;

            var userVC = ctx.Member.VoiceState.Channel;
            var lavalinkInstance = ctx.Client.GetLavalink();

            await ChannelAndConnection(ctx, userVC, lavalinkInstance);
            if(userVC == null || !lavalinkInstance.ConnectedNodes.Any() || userVC.Type != ChannelType.Voice)
                return;

            var node = lavalinkInstance.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            await Connection(ctx, conn);

            if(conn == null || conn.CurrentState.CurrentTrack == null)
                return;

            await conn.StopAsync();
            await conn.DisconnectAsync();

            var stoppedEmbed = new DiscordEmbedBuilder()
            {
                Color = DiscordColor.Purple,
                Title = "Track stopped"
            };

            await ctx.Channel.SendMessageAsync(embed: stoppedEmbed);
        }

        [Command("skip")]
        public async Task Skip(CommandContext ctx, [RemainingText] string query)
        {
            await VoiceState(ctx);
            if(ctx.Member.VoiceState == null)
                return;

            var userVC = ctx.Member.VoiceState.Channel;
            var lavalinkInstance = ctx.Client.GetLavalink();

            await ChannelAndConnection(ctx, userVC, lavalinkInstance);
            if(userVC == null || !lavalinkInstance.ConnectedNodes.Any() || userVC.Type != ChannelType.Voice)
                return;

            var node = lavalinkInstance.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            await Connection(ctx, conn);

            if(conn == null || conn.CurrentState.CurrentTrack == null)
                return;

            shouldSkipTrack = true;
            await conn.StopAsync();

            var skippedEmbed = new DiscordEmbedBuilder()
            {
                Color = DiscordColor.Purple,
                Title = "Track skipped"
            };

            await ctx.Channel.SendMessageAsync(embed: skippedEmbed);
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

            if(trackQueue.Count == 1)
                disconnectTimer?.Dispose();
        }

        [Command("viewqueue")]
        public async Task ViewQueue(CommandContext ctx)
        {
            if(trackQueue.Any())
            {
                var embed = new DiscordEmbedBuilder
                {
                    Color = DiscordColor.Purple,
                    Title = "Current Queue",
                    Description = string.Join("\n", trackQueue.Select((track, index) => $"{index + 1}. {track.Title} - {track.Author}"))
                };

                await ctx.RespondAsync(embed: embed);
            }
            else
            {
                var embed = new DiscordEmbedBuilder
                {
                    Color = DiscordColor.Purple,
                    Title = "Queue is Empty"
                };

                await ctx.RespondAsync(embed: embed);
            }
        }

        private async Task LavalinkSocketOnTrackEnd(CommandContext ctx, LavalinkGuildConnection conn, TrackFinishEventArgs eventArgs)
        {
            if (shouldSkipTrack)
            {
                await PlayNextTrack(ctx, conn);
                shouldSkipTrack = false;
            }
            else if (conn.CurrentState.CurrentTrack == null)
                await PlayNextTrack(ctx, conn);
        }

        private async Task PlayNextTrack(CommandContext ctx, LavalinkGuildConnection conn)
        {
            if(trackQueue.Any())
            {
                var nextTrack = trackQueue.First();
                trackQueue.RemoveAt(0);

                var lavalinkInstance = ctx.Client.GetLavalink();
                var node = lavalinkInstance.ConnectedNodes.Values.First();
                var guild = ctx.Guild; 
                var guildConn = node.GetGuildConnection(guild); 

                await guildConn.PlayAsync(nextTrack);

                var nowPlayingEmbed = CreateNowPlayingEmbed(nextTrack);

                await ctx.Channel.SendMessageAsync(embed: nowPlayingEmbed);
            }
            else if(conn.CurrentState.CurrentTrack == null)
                StartDisconnectTimer(conn);
        }

        private void StartDisconnectTimer(LavalinkGuildConnection conn)
        {
            disconnectTimer = new Timer(async (_) =>
            {
                await conn.DisconnectAsync();
                disconnectTimer.Dispose();
            }, null, disconnectDelayInSeconds * 1000, Timeout.Infinite);
        }

        private async Task VoiceState(CommandContext ctx)
        {
            if(ctx.Member.VoiceState == null)  
                await ctx.Channel.SendMessageAsync("You need to enter a VC");
        }

        private async Task ChannelAndConnection(CommandContext ctx, DiscordChannel userVC, LavalinkExtension lavalinkInstance)
        {
            if(userVC == null)
                await ctx.Channel.SendMessageAsync("You need to enter a VC");
            else if(!lavalinkInstance.ConnectedNodes.Any())
                await ctx.Channel.SendMessageAsync("Connection is not established");
            else if(userVC.Type != ChannelType.Voice)
                await ctx.Channel.SendMessageAsync("You need to enter a valid VC");
        }

        private async Task Connection(CommandContext ctx, LavalinkGuildConnection conn)
        {
            if(conn == null)
                await ctx.Channel.SendMessageAsync("Connection failed");
            else if (conn.CurrentState.CurrentTrack == null)
                await ctx.Channel.SendMessageAsync("No track is playing now");
        }

        private DiscordEmbedBuilder CreateNowPlayingEmbed(LavalinkTrack track)
        {
            string musicDescription = $"Now playing: {track.Title} \n" +
                                      $"Author: {track.Author} \n" +
                                      $"Length: {track.Length} \n" +
                                      $"URL: {track.Uri}";

            return new DiscordEmbedBuilder()
            {
                Color = DiscordColor.Purple,
                Title = "Successfully playing next track",
                Description = musicDescription
            };
        }
    }
}
