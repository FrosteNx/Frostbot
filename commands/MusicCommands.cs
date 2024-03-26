using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using DSharpPlus.Lavalink.EventArgs;
using System;
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
        private Dictionary<ulong, bool> trackLoopingStates = new Dictionary<ulong, bool>();
        private LavalinkTrack loopTrack = null;

        [Command("play"), Priority(0)]
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

        [Command("play"), Priority(1)]
        public async Task PlayUrl(CommandContext ctx, [RemainingText] Uri url)
        {
            if(disconnectTimer != null)
                disconnectTimer?.Dispose();

            await VoiceState(ctx);
            if(ctx.Member.VoiceState == null)
                return;

            var userVC = ctx.Member.VoiceState.Channel;
            var lavalinkInstance = ctx.Client.GetLavalink();

            await ChannelAndConnection(ctx, userVC, lavalinkInstance);
            if (userVC == null || !lavalinkInstance.ConnectedNodes.Any() || userVC.Type != ChannelType.Voice)
                return;

            var node = lavalinkInstance.ConnectedNodes.Values.First();
            await node.ConnectAsync(userVC);

            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
            if (conn == null)
            {
                await ctx.Channel.SendMessageAsync("Connection failed");
                return;
            }

            conn.PlaybackFinished += async (connection, args) => await LavalinkSocketOnTrackEnd(ctx, conn, args);

            if(trackQueue.Any())
            {
                var searchQuery = await node.Rest.GetTracksAsync(url);
                if(searchQuery.LoadResultType != LavalinkLoadResultType.NoMatches && searchQuery.LoadResultType != LavalinkLoadResultType.LoadFailed)
                {
                    var musicTrack = searchQuery.Tracks.First();
                    trackQueue.Add(musicTrack);
                    await ctx.Channel.SendMessageAsync($"Added {musicTrack.Title} to the queue.");
                }
                else
                    await ctx.Channel.SendMessageAsync($"Failed to find track with provided query: {url}");
            }
            else
            {
                var searchQuery = await node.Rest.GetTracksAsync(url);
                if(searchQuery.LoadResultType == LavalinkLoadResultType.NoMatches || searchQuery.LoadResultType == LavalinkLoadResultType.LoadFailed)
                {
                    await ctx.Channel.SendMessageAsync($"Failed to find music with provided query: {url}");
                    return;
                }

                var musicTrack = searchQuery.Tracks.First();

                await conn.PlayAsync(musicTrack);

                var nowPlayingEmbed = CreateNowPlayingEmbed(musicTrack);

                await ctx.Channel.SendMessageAsync(embed: nowPlayingEmbed);
            }
        }

        [Command("pause")]
        public async Task Pause(CommandContext ctx)
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
        public async Task Resume(CommandContext ctx)
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
        public async Task Stop(CommandContext ctx)
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
        public async Task Skip(CommandContext ctx)
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

        [Command("seek")]
        public async Task Seek(CommandContext ctx, TimeSpan position)
        {
            var lavalinkInstance = ctx.Client.GetLavalink();
            var node = lavalinkInstance.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            await Connection(ctx, conn);
            if(conn == null)
                return;

            if(conn.CurrentState.CurrentTrack == null)
            {
                await ctx.Channel.SendMessageAsync("No track is currently playing");
                return;
            }

            if(position.TotalMilliseconds < 0 || position > conn.CurrentState.CurrentTrack.Length)
            {
                await ctx.Channel.SendMessageAsync("Invalid seek position");
                return;
            }

            await conn.SeekAsync(position);

            var seekEmbed = new DiscordEmbedBuilder()
            {
                Color = DiscordColor.Purple,
                Title = "Seek",
                Description = $"Track seeked to {position}"
            };

            await ctx.Channel.SendMessageAsync(embed: seekEmbed);
        }

        [Command("forward")]
        public async Task Forward(CommandContext ctx, TimeSpan amount)
        {
            var lavalinkInstance = ctx.Client.GetLavalink();
            var node = lavalinkInstance.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            await Connection(ctx, conn);
            if(conn == null)
                return;

            if(conn.CurrentState.CurrentTrack == null)
            {
                await ctx.Channel.SendMessageAsync("No track is currently playing");
                return;
            }

            var currentPosition = conn.CurrentState.PlaybackPosition;
            var newPosition = currentPosition + amount;

            if(newPosition > conn.CurrentState.CurrentTrack.Length)
            {
                await ctx.Channel.SendMessageAsync("Cannot forward beyond the end of the track");
                return;
            }

            await conn.SeekAsync(newPosition);

            var forwardEmbed = new DiscordEmbedBuilder()
            {
                Color = DiscordColor.Purple,
                Title = "Forward",
                Description = $"Track forwarded by {amount}."
            };

            await ctx.Channel.SendMessageAsync(embed: forwardEmbed);
        }

        [Command("rewind")]
        public async Task Rewind(CommandContext ctx, TimeSpan amount)
        {
            var lavalinkInstance = ctx.Client.GetLavalink();
            var node = lavalinkInstance.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            await Connection(ctx, conn);
            if(conn == null)
                return;

            if(conn.CurrentState.CurrentTrack == null)
            {
                await ctx.Channel.SendMessageAsync("No track is currently playing");
                return;
            }

            var currentPosition = conn.CurrentState.PlaybackPosition;
            var newPosition = currentPosition - amount;

            if(newPosition < TimeSpan.Zero)
            {
                await ctx.Channel.SendMessageAsync("Cannot rewind beyond the start of the track");
                return;
            }

            await conn.SeekAsync(newPosition);

            var rewindEmbed = new DiscordEmbedBuilder()
            {
                Color = DiscordColor.Purple,
                Title = "Rewind",
                Description = $"Track rewinded by {amount}."
            };

            await ctx.Channel.SendMessageAsync(embed: rewindEmbed);
        }

        [Command("repeat")]
        public async Task Reapeat(CommandContext ctx)
        {
            await VoiceState(ctx);
            if (ctx.Member.VoiceState == null)
                return;

            var userVC = ctx.Member.VoiceState.Channel;
            var lavalinkInstance = ctx.Client.GetLavalink();

            await ChannelAndConnection(ctx, userVC, lavalinkInstance);
            if (userVC == null || !lavalinkInstance.ConnectedNodes.Any() || userVC.Type != ChannelType.Voice)
                return;

            var node = lavalinkInstance.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            await Connection(ctx, conn);
            if (conn == null || conn.CurrentState.CurrentTrack == null)
                return;

            var currentTrack = conn.CurrentState.CurrentTrack;
            await conn.PlayAsync(currentTrack);

            var repeatEmbed = new DiscordEmbedBuilder
            {
                Color = DiscordColor.Purple,
                Title = "Current track is now repeating"
            };

            await ctx.Channel.SendMessageAsync(embed: repeatEmbed);
        }

        [Command("loop")]
        public async Task Loop(CommandContext ctx)
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

            if(trackLoopingStates.ContainsKey(ctx.Guild.Id) && trackLoopingStates[ctx.Guild.Id])
            {
                var loopEmbed_ = new DiscordEmbedBuilder
                {
                    Color = DiscordColor.Purple,
                    Title = "Track looping is already enabled"
                };

                await ctx.Channel.SendMessageAsync(embed: loopEmbed_);
                return;
            }

            trackLoopingStates[ctx.Guild.Id] = true;
            loopTrack = conn.CurrentState.CurrentTrack;

            var loopEmbed = new DiscordEmbedBuilder
            {
                Color = DiscordColor.Purple,
                Title = "Track looping is now enabled"
            };

            await ctx.Channel.SendMessageAsync(embed: loopEmbed);
        }

        [Command("unloop")]
        public async Task Unloop(CommandContext ctx)
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

            if(!trackLoopingStates.ContainsKey(ctx.Guild.Id) && !trackLoopingStates[ctx.Guild.Id])
            {
                var unloopEmbed_ = new DiscordEmbedBuilder
                {
                    Color = DiscordColor.Purple,
                    Title = "Track looping is already disabled"
                };

                await ctx.Channel.SendMessageAsync(embed: unloopEmbed_);
                return;
            }

            trackLoopingStates[ctx.Guild.Id] = false;
            loopTrack = null;

            var unloopEmbed = new DiscordEmbedBuilder
            {
                Color = DiscordColor.Purple,
                Title = "Track looping is now disabled"
            };

            await ctx.Channel.SendMessageAsync(embed: unloopEmbed);
        }

        [Command("queue"), Priority(0)]
        public async Task QueueFromQuery(CommandContext ctx, [RemainingText] string query)
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

        [Command("queue"), Priority(1)]
        public async Task QueueFromURL(CommandContext ctx, Uri url)
        {
            var lavalinkInstance = ctx.Client.GetLavalink();
            var node = lavalinkInstance.ConnectedNodes.Values.First();

            var searchQuery = await node.Rest.GetTracksAsync(url);

            if (searchQuery.LoadResultType != LavalinkLoadResultType.NoMatches && searchQuery.LoadResultType != LavalinkLoadResultType.LoadFailed)
            {
                var musicTrack = searchQuery.Tracks.First();
                trackQueue.Add(musicTrack);
                await ctx.Channel.SendMessageAsync($"Added {musicTrack.Title} to the queue.");
            }
            else
                await ctx.Channel.SendMessageAsync($"Failed to find track with provided URL: {url}");

            if (trackQueue.Count == 1)
                disconnectTimer?.Dispose();
        }

        [Command("remove")]
        public async Task Remove(CommandContext ctx, int index)
        {
            if(index < 1 || index > trackQueue.Count)
            {
                await ctx.Channel.SendMessageAsync("Invalid track index");
                return;
            }

            var removedTrack = trackQueue[index - 1];
            trackQueue.RemoveAt(index - 1);

            var removeEmbed = new DiscordEmbedBuilder()
            {
                Color = DiscordColor.Purple,
                Title = $"Removed track at index {index}",
                Description = $"{removedTrack.Title} - {removedTrack.Author}"
            };

            await ctx.Channel.SendMessageAsync(embed: removeEmbed);
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

        [Command("volume")]
        public async Task SetVolume(CommandContext ctx, int volume)
        {
            if(volume < 0 || volume > 500)
            {
                await ctx.Channel.SendMessageAsync("Volume must be between 0 and 500");
                return;
            }

            var lavalinkInstance = ctx.Client.GetLavalink();
            var node = lavalinkInstance.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            await Connection(ctx, conn);
            if(conn == null)
                return;

            await conn.SetVolumeAsync(volume);

            var volumeEmbed = new DiscordEmbedBuilder()
            {
                Color = DiscordColor.Purple,
                Title = "Volume Set",
                Description = $"Volume set to {volume}%"
            };

            await ctx.Channel.SendMessageAsync(embed: volumeEmbed);
        }

        private async Task LavalinkSocketOnTrackEnd(CommandContext ctx, LavalinkGuildConnection conn, TrackFinishEventArgs eventArgs)
        {
            if(shouldSkipTrack)
            {
                await PlayNextTrack(ctx, conn);
                shouldSkipTrack = false;
            }
            else if(conn.CurrentState.CurrentTrack == null)
            {
                if(trackLoopingStates.ContainsKey(ctx.Guild.Id) && trackLoopingStates[ctx.Guild.Id] && loopTrack != null)
                {
                    await conn.PlayAsync(loopTrack);
                    return;
                }

                await PlayNextTrack(ctx, conn);
            }
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
