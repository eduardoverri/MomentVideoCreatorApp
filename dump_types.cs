using System;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.IO;

class Program
{
    static void Main()
    {
        var peReader = new PEReader(File.OpenRead(@"C:\Users\eduar\.nuget\packages\drastic.ffmpegkit.full.gpl.android\5.1.1\lib\net7.0-android33.0\Drastic.FFMpegKit.Android.dll"));
        var mdReader = peReader.GetMetadataReader();
        foreach (var typeDefHandle in mdReader.TypeDefinitions) {
            var typeDef = mdReader.GetTypeDefinition(typeDefHandle);
            Console.WriteLine(mdReader.GetString(typeDef.Namespace) + "." + mdReader.GetString(typeDef.Name));
        }
    }
}
