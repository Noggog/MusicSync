using Loqui;
using MusicRecord;

namespace Loqui
{
    public class ProtocolDefinition_MusicRecord : IProtocolRegistration
    {
        public readonly static ProtocolKey ProtocolKey = new ProtocolKey("MusicRecord");
        public void Register()
        {
            LoquiRegistration.Register(MusicRecord.Internals.Album_Registration.Instance);
            LoquiRegistration.Register(MusicRecord.Internals.Track_Registration.Instance);
            LoquiRegistration.Register(MusicRecord.Internals.Artist_Registration.Instance);
            LoquiRegistration.Register(MusicRecord.Internals.Cache_Registration.Instance);
            LoquiRegistration.Register(MusicRecord.Internals.AlbumCacheItem_Registration.Instance);
        }
    }
}
