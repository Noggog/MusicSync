using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web.Enums;
using SpotifyAPI.Web.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GoogleOutputToStandard
{
    class Program
    {
        private static string _clientId;
        private static string _secretId;
        private static string repo = @"C:\Users\Levia\Documents\Repos\MusicRecord";
        private static string file = @"C:\Users\Levia\Documents\Test.txt";
        static SpotifyWebAPI api;

        static void Main(string[] args)
        {
            _clientId = string.IsNullOrEmpty(_clientId)
                ? System.Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID")
                : _clientId;

            _secretId = string.IsNullOrEmpty(_secretId)
                ? System.Environment.GetEnvironmentVariable("SPOTIFY_SECRET_ID")
                : _secretId;

            AuthorizationCodeAuth auth =
                new AuthorizationCodeAuth(_clientId, _secretId, "http://localhost:4002", "http://localhost:4002",
                    Scope.PlaylistReadPrivate
                    | Scope.PlaylistReadCollaborative
                    | Scope.UserLibraryRead
                    | Scope.PlaylistModifyPrivate
                    | Scope.PlaylistModifyPublic);
            auth.AuthReceived += AuthOnAuthReceived;
            auth.Start();
            auth.OpenBrowser();

            Console.ReadLine();
        }

        private static async void AuthOnAuthReceived(object sender, AuthorizationCode payload)
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

            List<List<string>> missingTracks = new List<List<string>>();
            List<FullTrack> tracks = new List<FullTrack>();

            Dictionary<(string Album, string Artist), List<FullAlbum>> albumDict = new Dictionary<(string Album, string Artist), List<FullAlbum>>();

            foreach (var songLines in Split(File.ReadAllLines(file)))
            {
                var artist = songLines[1];
                var song = songLines[0];
                var albumName = songLines[2];
                bool? liked = null;
                if (songLines.Count > 3)
                {
                    if (songLines[3].Equals("5"))
                    {
                        liked = true;
                    }
                    else if (songLines[3].Equals("1"))
                    {
                        liked = false;
                    }
                    else if (songLines[3].Equals("0"))
                    {
                        liked = null;
                    }
                }
                if (!(liked ?? false)) continue;

                if (!albumDict.TryGetValue((albumName, artist), out var fullAlbums))
                {
                    var search = await api.SearchItemsAsync($"{albumName}", SearchType.Album, market: "US");
                    var results = await api.FlattenPageAsync<SearchItem, SimpleAlbum>(search.Albums, s => s.Albums);
                    var fullAlbumDownload = await Task.WhenAll(
                        results
                        .Where(t => t.Name.Equals(albumName))
                        .Select(a => api.GetAlbumAsync(a.Id)));
                    fullAlbums = fullAlbumDownload
                        .Where(al => al.Artists.Any(ar => ar.Name.Equals(artist)))
                        .ToList();
                    albumDict[(albumName, artist)] = fullAlbums;
                }
                if (fullAlbums == null)
                {
                    missingTracks.Add(songLines);
                    continue;
                }
                bool found = false;
                foreach (var album in fullAlbums)
                {
                    var albumTracks = await api.FlattenPageAsync<FullAlbum, SimpleTrack>(album.Tracks, s => s.Tracks);
                    var simpleTrack = albumTracks
                        .FirstOrDefault(t => t.Name.Equals(song));
                    if (simpleTrack == null)
                    {
                        continue;
                    }
                    var fullTrack = await api.GetTrackAsync(simpleTrack.Id);
                    if (fullTrack == null)
                    {
                        continue;
                    }
                    found = true;
                    tracks.Add(fullTrack);
                    break;
                }
                if (!found)
                {
                    missingTracks.Add(songLines);
                }
            }
        }

        static IEnumerable<List<string>> Split(IEnumerable<string> strs)
        {
            List<string> list = new List<string>();
            foreach (var str in strs)
            {
                if (str.Equals("|="))
                {
                    if (list.Count > 0)
                    {
                        yield return list;
                        list = new List<string>();
                    }
                }
                else
                {
                    list.Add(str);
                }
            }
        }
    }
}
