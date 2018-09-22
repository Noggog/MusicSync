using Loqui;
using Loqui.Generation;
using System;
using System.IO;

namespace Generation
{
    class Program
    {
        static void Main(string[] args)
        {
            LoquiGenerator gen = new LoquiGenerator()
            {
                RaisePropertyChangedDefault = false,
                NotifyingDefault = NotifyingType.None,
                ObjectCentralizedDefault = true,
                HasBeenSetDefault = false
            };
            gen.XmlTranslation.ShouldGenerateXSD = false;
            gen.XmlTranslation.ExportWithIGetter = false;

            var bethesdaProto = gen.AddProtocol(
                new ProtocolGeneration(
                    gen,
                    new ProtocolKey("MusicRecord"),
                    new DirectoryInfo("../../../../MusicSyncConsole"))
                {
                    DefaultNamespace = "MusicRecord",
                });

            gen.Generate().Wait();
        }
    }
}
