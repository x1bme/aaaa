namespace Archiver
{
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    internal static class FileWithEncryption
    {
        internal static void WriteAllBytes( string path, byte[] bytes )
        {
            var fStream = new FileStream( path, FileMode.Create, FileAccess.Write );
            var cStream = new CryptoStream( fStream, Obfuscator.CreateEncryptor( ), CryptoStreamMode.Write );
            var writer = new BinaryWriter( cStream );
            writer.Write( bytes );
            cStream.FlushFinalBlock( );
            writer.Flush( );
            writer.Dispose( );
        }

        internal static byte[] ReadAllBytes( string path )
        {
            var data = File.ReadAllBytes( path );
            var mStream = new MemoryStream( );
            var cStream = new CryptoStream( mStream, Obfuscator.CreateDecryptor( ), CryptoStreamMode.Write );
            cStream.Write( data, 0, data.Length );
            cStream.FlushFinalBlock( );
            var result = mStream.ToArray( );
            cStream.Dispose( );
            return result;
        }

        internal static void WriteAllText( string path, string content )
        {
            var bits = Encoding.Unicode.GetBytes( content );
            WriteAllBytes( path, bits );
        }

        // internal static void WriteAllTextToPath( string path, string content )
        // {
        //     using ( var fStream = new FileStream( path, FileMode.Create, FileAccess.Write ) )
        //     using ( var writer = new StreamWriter( fStream, Encoding.Unicode ) )
        //     {
        //         writer.Write( content );
        //     }
        // }


        internal static string ReadAllText( string path )
        {
            var bits = ReadAllBytes( path );
            return Encoding.Unicode.GetString( bits );
        }

        internal static void RewriteFile( string path )
        {
            var bits = ReadAllBytes( path );
            File.WriteAllBytes( path, bits );
        }
    }
}
