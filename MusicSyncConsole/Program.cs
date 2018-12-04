using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web.Enums;
using SpotifyAPI.Web.Models;
using System;
using System.Threading.Tasks;
using Noggog.MusicSync.Spotify;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using LibGit2Sharp;
using System.IO;
using MusicRecord;
using Noggog.Utility;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace MusicSyncConsole
{
    class Program
    {
        private static string _clientId;
        private static string _secretId;
        private static string _thumbsDown = "49kfhsj1i1QY6ReZ2Y7EWf";
        private static string _repoLocation = @"C:\Users\Levia\Documents\Repos\MusicRecord";
        private static string _cacheLocation = @"C:\Users\Levia\Documents\Repos\MusicRecord\.git\MusicSyncCache";
        private static string _repoUrl = @"https://github.com/Noggog/MusicRecord.git";

        private static string _napalm = @"4vMyproswWLGLY2QglWa12";
        private static string _coolPool = @"1K2vydsNnFWWLwSNwsfu1V";
        private static string _campfire = @"3tbNDbWdK7UOkyVWuLxOeo";
        private static string _remove = @"4dwn8lMc8772d69qNxIH4u";

        private static Cache cache;

        private static string _doom = @"5teayAvamzg8z0PqxdsXUX";
        private static List<PlaylistTrack> doomTracks;
        private static HashSet<string> doomTrackHash;
        private static string _chill = @"39kHuIuAU0nYXPdCWUl1Vy";
        private static string _chill2 = @"41MEIZlbCGjndFfXPfbX4l";
        private static List<PlaylistTrack> chillTracks;
        private static HashSet<string> chillTrackHash;
        private static string _ambient = @"44DTXh42c8OucRcS36W1A2";
        private static List<PlaylistTrack> ambientTracks;

        private static PrivateProfile profile;
        private static SpotifyWebAPI api;
        private static HashSet<string> savedTracksHash;

        private static TaskCompletionSource<bool> done = new TaskCompletionSource<bool>();

        static async Task Main(string[] args)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
            _clientId = configuration["SPOTIFY_CLIENT_ID"];
            _secretId = configuration["SPOTIFY_SECRET_ID"];

            System.Console.WriteLine($"Spotify Client ID: {_clientId}");

            if (File.Exists(_cacheLocation))
            {
                cache = Cache.Create_Xml(_cacheLocation);
            }
            else
            {
                cache = new Cache();
            }

            AuthorizationCodeAuth auth =
                new AuthorizationCodeAuth(_clientId, _secretId, "http://localhost:4002", "http://localhost:4002",
                    Scope.PlaylistReadPrivate
                    | Scope.PlaylistReadCollaborative
                    | Scope.UserLibraryRead
                    | Scope.PlaylistModifyPrivate
                    | Scope.PlaylistModifyPublic);
            auth.AuthReceived += async (object sender, AuthorizationCode payload) =>
            {
                try
                {
                    await AuthOnAuthReceived(sender, payload);
                }
                catch (Exception ex)
                {
                    System.Console.Write(ex);
                    done.SetResult(false);
                }
            };
            auth.Start();
            auth.OpenBrowser();

            await done.Task;
        }

        private static async Task AuthOnAuthReceived(object sender, AuthorizationCode payload)
        {
            AuthorizationCodeAuth auth = (AuthorizationCodeAuth)sender;
            auth.Stop();

            Token token = await auth.ExchangeCode(payload.Code);
            api = new SpotifyWebAPI()
            {
                AccessToken = token.AccessToken,
                TokenType = token.TokenType,
                UseAutoRetry = true,
            };

            System.Console.WriteLine("Getting private profile.");
            profile = await api.GetPrivateProfileAsync();

            // Get Tracks
            System.Console.WriteLine("Getting saved tracks.");
            var savedTracks = await api.FlattenPageAsync(await api.GetSavedTracksAsync());
            savedTracksHash = savedTracks
                .Select(t => t.Track.Id)
                .ToHashSet();

            System.Console.WriteLine("Getting doom tracks.");
            doomTracks = (await api.FlattenPageAsync(
                await api.GetPlaylistTracksAsync(
                    profile.Id,
                    _doom)))
                    .ToList();
            doomTrackHash = doomTracks.Select(t => t.Track.Id).ToHashSet();

            System.Console.WriteLine("Getting ambient tracks.");
            ambientTracks = (await api.FlattenPageAsync(
                await api.GetPlaylistTracksAsync(
                    profile.Id,
                    _ambient)))
                    .ToList();

            System.Console.WriteLine("Getting chill tracks.");
            chillTracks = (await api.FlattenPageAsync(
                await api.GetPlaylistTracksAsync(
                    profile.Id,
                    _chill)))
                    .ToList();
            chillTracks.AddRange((await api.FlattenPageAsync(
                await api.GetPlaylistTracksAsync(
                    profile.Id,
                    _chill2))));
            chillTrackHash = chillTracks.Select(t => t.Track.Id).ToHashSet();

            // Remove things
            System.Console.WriteLine("Handling removes.");
            await HandleRemoves(
                (await api.FlattenPageAsync(
                    await api.GetPlaylistTracksAsync(
                        profile.Id,
                        _remove))).ToList(),
                removeFromListItself: true);
            await HandleRemoves(
                (await api.FlattenPageAsync(
                    await api.GetPlaylistTracksAsync(
                        profile.Id,
                        _thumbsDown))).ToList(),
                removeFromListItself: false);

            // Refreshed genred source pools
            System.Console.WriteLine("Refreshing genre source pools.");
            await RefreshPlaylist(
                doomTracks
                .Select(p => p.Track)
                .Where(t => savedTracksHash.Contains(t.Id)),
                _napalm);
            await RefreshPlaylist(
                chillTracks
                .Select(p => p.Track)
                .Where(t => savedTracksHash.Contains(t.Id)),
                _coolPool);

            // Save state to Git
            System.Console.WriteLine("Saving to git.");
            if (!Repository.IsValid(_repoLocation))
            {
                Repository.Clone(_repoUrl, _repoLocation);
            }
            DirectoryInfo libraryFolder = new DirectoryInfo(
                Path.Combine(_repoLocation, "Library"));
            libraryFolder.Create();
            await ExportToFolder(
                libraryFolder,
                savedTracks.Select(s => s.Track)
                    .Concat(doomTracks.Select(s => s.Track)
                    .Concat(chillTracks.Select(s => s.Track))));

            //using (var repo = new Repository(_repoLocation))
            //{
            //    var status = repo.RetrieveStatus();
            //    foreach (var file in status.Modified.Concat(status.Untracked).Concat(status.Missing))
            //    {
            //        RepositoryExtensions.
            //    }
            //}

            System.Console.WriteLine("DONE");
            done.SetResult(true);
        }

        private static async Task HandleRemoves(
            List<PlaylistTrack> removeTracks,
            bool removeFromListItself)
        {
            var removeSet = removeTracks
                    .Select((t) => t.Track.Uri)
                    .ToHashSet();
            if (removeTracks.Count == 0) return;
            var failedRemoves = await Task.WhenAll(
                HandleRemoves(
                    removeSet,
                    _doom,
                    doomTracks),
                HandleRemoves(
                    removeSet,
                    _chill,
                    chillTracks),
                HandleRemoves(
                    removeSet,
                    _ambient,
                    ambientTracks));

            // ToDo
            // Remove from saved?

            if (removeFromListItself)
            {
                await HandleRemoves(
                    removeTracks
                        .Select(t => t.Track.Uri)
                        .Except(failedRemoves.SelectMany(e => e))
                        .ToHashSet(),
                    _remove,
                    removeTracks);
            }
        }

        private static async Task<IEnumerable<string>> HandleRemoves(
            HashSet<string> removeTracks,
            string playlistID,
            List<PlaylistTrack> tracks)
        {
            var toRemove = tracks
                .Where(t => removeTracks.Contains(t.Track.Uri))
                .Select(t => t.Track.Uri)
                .ToArray();
            if (toRemove.Length == 0) return Enumerable.Empty<string>();
            var errs = await Task.WhenAll(
                toRemove
                .Cut(99)
                .Select((c) =>
                {
                    return api.RemovePlaylistTracksAsync(
                        profile.Id,
                        playlistID,
                        c.Select(s => new DeleteTrackUri(s)).ToList());
                }));
            PrintErrors(errs);
            if (errs.Any(e => e.Error != null))
            {
                return toRemove;
            }
            for (int i = tracks.Count - 1; i >= 0; i--)
            {
                if (removeTracks.Contains(tracks[i].Track.Uri))
                {
                    tracks.RemoveAt(i);
                }
            }
            return Enumerable.Empty<string>();
        }

        private static async Task RefreshPlaylist(
            IEnumerable<FullTrack> sourceList,
            string playlistID)
        {
            var err = await api.ReplacePlaylistTracksAsync(
                profile.Id,
                playlistID,
                Enumerable.Empty<string>().ToList());
            if (err.Error != null) return;

            foreach (var cut in sourceList.Cut(75))
            {
                err = await api.AddPlaylistTracksAsync(
                    profile.Id,
                    playlistID,
                    cut.Select((p) => p.Uri).ToList());
                if (err.Error != null) return;
            }
        }

        //private static async Task RefreshPlaylist(
        //    IEnumerable<FullTrack> sourceList,
        //    string playlistID,
        //    int lengthMS = 36_000_000) // 10 hours
        //{
        //    List<FullTrack> options = sourceList
        //        .Where((i) => i.Loc)
        //        .ToList();
        //    List<FullTrack> picks = new List<FullTrack>(200);
        //    Random random = new Random();
        //    while (picks.Sum((p) => p.DurationMs) < lengthMS) // 10 hours
        //    {
        //        if (options.Count == 0) break;
        //        var pick = random.Next(options.Count);
        //        picks.Add(options[pick]);
        //        options.RemoveAt(pick);
        //    }

        //    var err = await api.ReplacePlaylistTracksAsync(
        //        profile.Id,
        //        playlistID,
        //        Enumerable.Empty<string>().ToList());
        //    if (err.Error != null) return;

        //    foreach (var cut in picks.Cut(75))
        //    {
        //        err = await api.AddPlaylistTracksAsync(
        //            profile.Id,
        //            playlistID,
        //            cut.Select((p) => p.Uri).ToList());
        //        if (err.Error != null) return;
        //    }
        //}

        private static void PrintErrors(params ErrorResponse[] errs)
        {
            foreach (var err in errs)
            {
                if (err.Error != null)
                {
                    System.Console.WriteLine(err.Error.Message);
                }
            }
        }

        private static async Task ExportToFolder(
            DirectoryInfo dir,
            IEnumerable<FullTrack> tracks)
        {
            using (new FolderCleaner(dir, FolderCleaner.CleanType.WriteTime))
            {
                Dictionary<string, Artist> dict = new Dictionary<string, Artist>();

                foreach (var album in tracks
                    .Distinct(t => t.Id)
                    .GroupBy(t => t.Album)
                    .OrderBy(a => a.Key.Name))
                {
                    if (!cache.Albums.TryGetValue(album.Key.Id, out var albumCache))
                    {
                        var fullAlbum = await api.GetAlbumAsync(album.Key.Id);
                        albumCache = new AlbumCacheItem()
                        {
                            ID = fullAlbum.Id,
                            ArtistsEnumerable = fullAlbum.Artists.Select(a => a.Name)
                        };
                        cache.Albums.Set(albumCache);
                    }
                    var artistNames = string.Join(", ", albumCache.Artists);
                    var ar = dict.TryCreateValue(artistNames, () => new Artist()
                    {
                        Name = artistNames
                    });
                    var al = ar.Albums.TryCreateValue(album.Key.Name, (k) => new Album()
                    {
                        Name = k,
                        SpotifyID = album.Key.Id
                    });
                    foreach (var t in album)
                    {
                        var tr = new Track()
                        {
                            Liked = savedTracksHash.Contains(t.Id),
                            Name = t.Name,
                            SpotifyID = t.Id,
                            TrackNumber = t.TrackNumber,
                            DiscNumber = t.DiscNumber
                        };
                        if (chillTrackHash.Contains(t.Id))
                        {
                            tr.Tags.Add("Light");
                        }
                        if (doomTrackHash.Contains(t.Id))
                        {
                            tr.Tags.Add("Heavy");
                        }
                        al.Tracks.Add(tr);
                    }
                }

                foreach (var artist in dict.Values)
                {
                    foreach (var album in artist.Albums.Values)
                    {
                        album.Tracks.SetTo(album.Tracks
                            .GroupBy(t => t.DiscNumber)
                            .OrderBy(g => g.Key)
                            .SelectMany(g => g.OrderBy(t => t.TrackNumber)));
                    }
                }

                foreach (var kv in dict
                    .OrderBy(kv => kv.Key))
                {
                    FileInfo file = new FileInfo($"{Path.Combine(dir.FullName, MakeValidFileName(kv.Key))}.xml");
                    file.Directory.Create();
                    kv.Value.Write_Xml(file.FullName);
                    file.LastAccessTime = DateTime.Now;
                }

                cache.Write_Xml(_cacheLocation);
            }
        }

        private static string MakeValidFileName(string name)
        {
            string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());

            foreach (char c in invalid)
            {
                name = name.Replace(c.ToString(), "");
            }
            return name;
        }
    }
}
