//
// Serializer.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2021 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using MonoTorrent.BEncoding;
using MonoTorrent.Connections;
using MonoTorrent.PieceWriter;

namespace MonoTorrent.Client
{
    static class Serializer
    {
        internal static EngineSettings DeserializeEngineSettings (BEncodedDictionary dictionary)
            => DeserializeEngineSettingsBuilder (dictionary).ToSettings ();

        internal static BEncodedDictionary Serialize (EngineSettings settings)
            => Serialize (new EngineSettingsBuilder (settings));

        internal static TorrentSettings DeserializeTorrentSettings (BEncodedDictionary dictionary)
            => DeserializeTorrentSettingsBuilder (dictionary).ToSettings ();

        internal static BEncodedDictionary Serialize (TorrentSettings settings)
            => Serialize (new TorrentSettingsBuilder (settings));

        static EngineSettingsBuilder DeserializeEngineSettingsBuilder (BEncodedDictionary dict)
        {
            var builder = new EngineSettingsBuilder ();

            if (TryGetValue (dict, nameof (EngineSettingsBuilder.AllowedEncryption), ToEncryptionTypeList, out List<EncryptionType>? allowedEncryption))
                builder.AllowedEncryption = allowedEncryption;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.AllowHaveSuppression), ToBoolean, out bool allowHaveSuppression))
                builder.AllowHaveSuppression = allowHaveSuppression;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.AllowLocalPeerDiscovery), ToBoolean, out bool allowLocalPeerDiscovery))
                builder.AllowLocalPeerDiscovery = allowLocalPeerDiscovery;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.AllowPortForwarding), ToBoolean, out bool allowPortForwarding))
                builder.AllowPortForwarding = allowPortForwarding;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.AutoSaveLoadDhtCache), ToBoolean, out bool autoSaveLoadDhtCache))
                builder.AutoSaveLoadDhtCache = autoSaveLoadDhtCache;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.AutoSaveLoadFastResume), ToBoolean, out bool autoSaveLoadFastResume))
                builder.AutoSaveLoadFastResume = autoSaveLoadFastResume;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.AutoSaveLoadMagnetLinkMetadata), ToBoolean, out bool autoSaveLoadMagnetLinkMetadata))
                builder.AutoSaveLoadMagnetLinkMetadata = autoSaveLoadMagnetLinkMetadata;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.CacheDirectory), ToStringValue, out string? cacheDirectory))
                builder.CacheDirectory = cacheDirectory;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.ConnectionRetryDelays), ToTimeSpanList, out List<TimeSpan>? connectionRetryDelays))
                builder.ConnectionRetryDelays = connectionRetryDelays;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.ConnectionTimeout), ToTimeSpan, out TimeSpan connectionTimeout))
                builder.ConnectionTimeout = connectionTimeout;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.DhtEndPoint), ToEndPoint, out IPEndPoint? dhtEndPoint))
                builder.DhtEndPoint = dhtEndPoint;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.DiskCacheBytes), ToInt32, out int diskCacheBytes))
                builder.DiskCacheBytes = diskCacheBytes;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.DiskCachePolicy), value => ToEnum<CachePolicy> (value), out CachePolicy diskCachePolicy))
                builder.DiskCachePolicy = diskCachePolicy;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.FastResumeMode), value => ToEnum<FastResumeMode> (value), out FastResumeMode fastResumeMode))
                builder.FastResumeMode = fastResumeMode;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.FileCreationMode), value => ToEnum<FileCreationOptions> (value), out FileCreationOptions fileCreationMode))
                builder.FileCreationMode = fileCreationMode;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.HttpStreamingPrefix), ToStringValue, out string? httpStreamingPrefix))
                builder.HttpStreamingPrefix = httpStreamingPrefix;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.ListenEndPoints), ToIPAddressDictionary, out Dictionary<string, IPEndPoint>? listenEndPoints))
                builder.ListenEndPoints = listenEndPoints;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.MaximumConnections), ToInt32, out int maximumConnections))
                builder.MaximumConnections = maximumConnections;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.MaximumDiskReadRate), ToInt32, out int maximumDiskReadRate))
                builder.MaximumDiskReadRate = maximumDiskReadRate;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.MaximumDiskWriteRate), ToInt32, out int maximumDiskWriteRate))
                builder.MaximumDiskWriteRate = maximumDiskWriteRate;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.MaximumDownloadRate), ToInt32, out int maximumDownloadRate))
                builder.MaximumDownloadRate = maximumDownloadRate;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.MaximumHalfOpenConnections), ToInt32, out int maximumHalfOpenConnections))
                builder.MaximumHalfOpenConnections = maximumHalfOpenConnections;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.MaximumOpenFiles), ToInt32, out int maximumOpenFiles))
                builder.MaximumOpenFiles = maximumOpenFiles;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.MaximumUploadRate), ToInt32, out int maximumUploadRate))
                builder.MaximumUploadRate = maximumUploadRate;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.ReportedListenEndPoints), ToIPAddressDictionary, out Dictionary<string, IPEndPoint>? reportedListenEndPoints))
                builder.ReportedListenEndPoints = reportedListenEndPoints;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.StaleRequestTimeout), ToTimeSpan, out TimeSpan staleRequestTimeout))
                builder.StaleRequestTimeout = staleRequestTimeout;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.UsePartialFiles), ToBoolean, out bool usePartialFiles))
                builder.UsePartialFiles = usePartialFiles;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.WebSeedConnectionTimeout), ToTimeSpan, out TimeSpan webSeedConnectionTimeout))
                builder.WebSeedConnectionTimeout = webSeedConnectionTimeout;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.WebSeedDelay), ToTimeSpan, out TimeSpan webSeedDelay))
                builder.WebSeedDelay = webSeedDelay;
            if (TryGetValue (dict, nameof (EngineSettingsBuilder.WebSeedSpeedTrigger), ToInt32, out int webSeedSpeedTrigger))
                builder.WebSeedSpeedTrigger = webSeedSpeedTrigger;

            return builder;
        }

        static TorrentSettingsBuilder DeserializeTorrentSettingsBuilder (BEncodedDictionary dict)
        {
            var builder = new TorrentSettingsBuilder ();

            if (TryGetValue (dict, nameof (TorrentSettingsBuilder.AllowDht), ToBoolean, out bool allowDht))
                builder.AllowDht = allowDht;
            if (TryGetValue (dict, nameof (TorrentSettingsBuilder.AllowInitialSeeding), ToBoolean, out bool allowInitialSeeding))
                builder.AllowInitialSeeding = allowInitialSeeding;
            if (TryGetValue (dict, nameof (TorrentSettingsBuilder.AllowPeerExchange), ToBoolean, out bool allowPeerExchange))
                builder.AllowPeerExchange = allowPeerExchange;
            if (TryGetValue (dict, nameof (TorrentSettingsBuilder.CreateContainingDirectory), ToBoolean, out bool createContainingDirectory))
                builder.CreateContainingDirectory = createContainingDirectory;
            if (TryGetValue (dict, nameof (TorrentSettingsBuilder.MaximumConnections), ToInt32, out int maximumConnections))
                builder.MaximumConnections = maximumConnections;
            if (TryGetValue (dict, nameof (TorrentSettingsBuilder.MaximumDownloadRate), ToInt32, out int maximumDownloadRate))
                builder.MaximumDownloadRate = maximumDownloadRate;
            if (TryGetValue (dict, nameof (TorrentSettingsBuilder.MaximumUploadRate), ToInt32, out int maximumUploadRate))
                builder.MaximumUploadRate = maximumUploadRate;
            if (TryGetValue (dict, nameof (TorrentSettingsBuilder.RequirePeerIdToMatch), ToBoolean, out bool requirePeerIdToMatch))
                builder.RequirePeerIdToMatch = requirePeerIdToMatch;
            if (TryGetValue (dict, nameof (TorrentSettingsBuilder.UploadSlots), ToInt32, out int uploadSlots))
                builder.UploadSlots = uploadSlots;

            return builder;
        }

        static BEncodedDictionary Serialize (EngineSettingsBuilder builder)
        {
            return new BEncodedDictionary {
                [nameof (EngineSettingsBuilder.AllowedEncryption)] = FromEncryptionTypeList (builder.AllowedEncryption),
                [nameof (EngineSettingsBuilder.AllowHaveSuppression)] = FromBoolean (builder.AllowHaveSuppression),
                [nameof (EngineSettingsBuilder.AllowLocalPeerDiscovery)] = FromBoolean (builder.AllowLocalPeerDiscovery),
                [nameof (EngineSettingsBuilder.AllowPortForwarding)] = FromBoolean (builder.AllowPortForwarding),
                [nameof (EngineSettingsBuilder.AutoSaveLoadDhtCache)] = FromBoolean (builder.AutoSaveLoadDhtCache),
                [nameof (EngineSettingsBuilder.AutoSaveLoadFastResume)] = FromBoolean (builder.AutoSaveLoadFastResume),
                [nameof (EngineSettingsBuilder.AutoSaveLoadMagnetLinkMetadata)] = FromBoolean (builder.AutoSaveLoadMagnetLinkMetadata),
                [nameof (EngineSettingsBuilder.CacheDirectory)] = new BEncodedString (builder.CacheDirectory),
                [nameof (EngineSettingsBuilder.ConnectionRetryDelays)] = FromTimeSpanList (builder.ConnectionRetryDelays),
                [nameof (EngineSettingsBuilder.ConnectionTimeout)] = FromTimeSpan (builder.ConnectionTimeout),
                [nameof (EngineSettingsBuilder.DhtEndPoint)] = FromEndPoint (builder.DhtEndPoint),
                [nameof (EngineSettingsBuilder.DiskCacheBytes)] = new BEncodedNumber (builder.DiskCacheBytes),
                [nameof (EngineSettingsBuilder.DiskCachePolicy)] = FromEnum (builder.DiskCachePolicy),
                [nameof (EngineSettingsBuilder.FastResumeMode)] = FromEnum (builder.FastResumeMode),
                [nameof (EngineSettingsBuilder.FileCreationMode)] = FromEnum (builder.FileCreationMode),
                [nameof (EngineSettingsBuilder.HttpStreamingPrefix)] = new BEncodedString (builder.HttpStreamingPrefix),
                [nameof (EngineSettingsBuilder.ListenEndPoints)] = FromIPAddressDictionary (builder.ListenEndPoints),
                [nameof (EngineSettingsBuilder.MaximumConnections)] = new BEncodedNumber (builder.MaximumConnections),
                [nameof (EngineSettingsBuilder.MaximumDiskReadRate)] = new BEncodedNumber (builder.MaximumDiskReadRate),
                [nameof (EngineSettingsBuilder.MaximumDiskWriteRate)] = new BEncodedNumber (builder.MaximumDiskWriteRate),
                [nameof (EngineSettingsBuilder.MaximumDownloadRate)] = new BEncodedNumber (builder.MaximumDownloadRate),
                [nameof (EngineSettingsBuilder.MaximumHalfOpenConnections)] = new BEncodedNumber (builder.MaximumHalfOpenConnections),
                [nameof (EngineSettingsBuilder.MaximumOpenFiles)] = new BEncodedNumber (builder.MaximumOpenFiles),
                [nameof (EngineSettingsBuilder.MaximumUploadRate)] = new BEncodedNumber (builder.MaximumUploadRate),
                [nameof (EngineSettingsBuilder.ReportedListenEndPoints)] = FromIPAddressDictionary (builder.ReportedListenEndPoints),
                [nameof (EngineSettingsBuilder.StaleRequestTimeout)] = FromTimeSpan (builder.StaleRequestTimeout),
                [nameof (EngineSettingsBuilder.UsePartialFiles)] = FromBoolean (builder.UsePartialFiles),
                [nameof (EngineSettingsBuilder.WebSeedConnectionTimeout)] = FromTimeSpan (builder.WebSeedConnectionTimeout),
                [nameof (EngineSettingsBuilder.WebSeedDelay)] = FromTimeSpan (builder.WebSeedDelay),
                [nameof (EngineSettingsBuilder.WebSeedSpeedTrigger)] = new BEncodedNumber (builder.WebSeedSpeedTrigger),
            };
        }

        static BEncodedDictionary Serialize (TorrentSettingsBuilder builder)
        {
            return new BEncodedDictionary {
                [nameof (TorrentSettingsBuilder.AllowDht)] = FromBoolean (builder.AllowDht),
                [nameof (TorrentSettingsBuilder.AllowInitialSeeding)] = FromBoolean (builder.AllowInitialSeeding),
                [nameof (TorrentSettingsBuilder.AllowPeerExchange)] = FromBoolean (builder.AllowPeerExchange),
                [nameof (TorrentSettingsBuilder.CreateContainingDirectory)] = FromBoolean (builder.CreateContainingDirectory),
                [nameof (TorrentSettingsBuilder.MaximumConnections)] = new BEncodedNumber (builder.MaximumConnections),
                [nameof (TorrentSettingsBuilder.MaximumDownloadRate)] = new BEncodedNumber (builder.MaximumDownloadRate),
                [nameof (TorrentSettingsBuilder.MaximumUploadRate)] = new BEncodedNumber (builder.MaximumUploadRate),
                [nameof (TorrentSettingsBuilder.RequirePeerIdToMatch)] = FromBoolean (builder.RequirePeerIdToMatch),
                [nameof (TorrentSettingsBuilder.UploadSlots)] = new BEncodedNumber (builder.UploadSlots),
            };
        }

        static bool TryGetValue<T> (BEncodedDictionary dictionary, string key, Func<BEncodedValue, T> parser, out T value)
        {
            if (dictionary.TryGetValue (key, out BEncodedValue? encodedValue)) {
                value = parser (encodedValue);
                return true;
            }

            value = default!;
            return false;
        }

        static bool ToBoolean (BEncodedValue value)
            => bool.Parse (((BEncodedString) value).Text);

        static int ToInt32 (BEncodedValue value)
            => (int) ((BEncodedNumber) value).Number;

        static string ToStringValue (BEncodedValue value)
            => ((BEncodedString) value).Text;

        static TimeSpan ToTimeSpan (BEncodedValue value)
            => TimeSpan.FromTicks (((BEncodedNumber) value).Number);

        static T ToEnum<T> (BEncodedValue value)
            where T : struct, Enum
            => (T) Enum.Parse (typeof (T), ((BEncodedString) value).Text);

        static List<EncryptionType> ToEncryptionTypeList (BEncodedValue value)
        {
            var result = new List<EncryptionType> ();
            foreach (BEncodedString encryptionType in (BEncodedList) value)
                result.Add ((EncryptionType) Enum.Parse (typeof (EncryptionType), encryptionType.Text));
            return result;
        }

        static IPEndPoint? ToEndPoint (BEncodedValue value)
        {
            var list = (BEncodedList) value;
            if (list.Count != 2)
                return null;

            var ipAddress = (BEncodedString) list.Single (t => t is BEncodedString);
            var port = (BEncodedNumber) list.Single (t => t is BEncodedNumber);
            return new IPEndPoint (IPAddress.Parse (ipAddress.Text), (int) port.Number);
        }

        static BEncodedString FromBoolean (bool value)
            => new BEncodedString (value.ToString ());

        static BEncodedString FromEnum<T> (T value)
            where T : struct, Enum
            => new BEncodedString (value.ToString ());

        static BEncodedNumber FromTimeSpan (TimeSpan value)
            => new BEncodedNumber (value.Ticks);

        static BEncodedList FromEncryptionTypeList (IList<EncryptionType> value)
            => new BEncodedList (value.Select (v => (BEncodedString) v.ToString ()));

        static BEncodedList FromTimeSpanList (IList<TimeSpan> value)
            => new BEncodedList (value.Select (v => (BEncodedNumber) v.Ticks));

        static BEncodedValue FromEndPoint (IPEndPoint? value)
            => value is null
                ? new BEncodedList ()
                : new BEncodedList { (BEncodedString) value.Address.ToString (), (BEncodedNumber) value.Port };

        static BEncodedDictionary FromIPAddressDictionary (IDictionary<string, IPEndPoint> value)
        {
            var result = new BEncodedDictionary ();
            foreach (var kvp in value)
                result[kvp.Key] = new BEncodedList { (BEncodedString) kvp.Value.Address.ToString (), (BEncodedNumber) kvp.Value.Port };
            return result;
        }

        static Dictionary<string, IPEndPoint> ToIPAddressDictionary (BEncodedValue value)
        {
            var result = new Dictionary<string, IPEndPoint> ();
            foreach (var kvp in (BEncodedDictionary) value) {
                var parts = (BEncodedList) kvp.Value;
                result[kvp.Key.Text] = new IPEndPoint (IPAddress.Parse (((BEncodedString) parts[0]).Text), (int) ((BEncodedNumber) parts[1]).Number);
            }
            return result;
        }

        static List<TimeSpan> ToTimeSpanList (BEncodedValue value)
        {
            var list = (BEncodedList) value;
            var result = new List<TimeSpan> (list.Count);
            foreach (BEncodedNumber number in list)
                result.Add (TimeSpan.FromTicks (number.Number));
            return result;
        }
    }
}
