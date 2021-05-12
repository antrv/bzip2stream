using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Data.Sqlite;

namespace Test
{
    internal static class Program
	{
        private static void Main(string[] args)
        {
            SqliteConnection c = new SqliteConnection();

			TestCompression(@"bzip2.htm"); // 130 KB
			TestCompression(@"osmbook.pdf"); // 11 MB
			TestCompression(@"archive.rar"); // 129 MB
			//DecompressPlanetFilePartially(@"Y:\Incoming\2014-02-25\planet-140212.osm.bz2");
		}

        private static void DecompressPlanetFilePartially(string filename)
        {
            using FileStream inputStream = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            using BZip2Stream compressStream = new(inputStream, CompressionMode.Decompress);
            using FileStream outputStream = new(filename+".xml", FileMode.Create);
            byte[] buffer = new byte[123456];
            while (inputStream.Position < 100 * 1024 * 1024) // 100 MB
            {
                int bytes = compressStream.Read(buffer, 0, buffer.Length);
                outputStream.Write(buffer, 0, bytes);
            }
        }

        private static void TestCompression(string inputFile)
		{
			Compress(inputFile, inputFile + ".bz2");
			Decompress(inputFile + ".bz2", inputFile + ".out");
			CompareFiles(inputFile, inputFile + ".out");
		}

        private static void Compress(string inputFile, string outputFile)
        {
            using FileStream inputStream = new(inputFile, FileMode.Open);
            using FileStream outputStream = new(outputFile, FileMode.Create);
            using BZip2Stream compressStream = new(outputStream, BZip2CompressionLevel.Lv9);
            inputStream.CopyTo(compressStream, 32768);
        }

        private static void Decompress(string inputFile, string outputFile)
        {
            using FileStream inputStream = new(inputFile, FileMode.Open);
            using BZip2Stream compressStream = new(inputStream, CompressionMode.Decompress);
            using FileStream outputStream = new(outputFile, FileMode.Create);
            compressStream.CopyTo(outputStream, 32768);
        }

        private static bool CompareFiles(string inputFile1, string inputFile2)
        {
            using FileStream inputStream1 = new(inputFile1, FileMode.Open);
            using FileStream inputStream2 = new(inputFile2, FileMode.Open);
            if (inputStream1.Length != inputStream2.Length)
            {
                Console.WriteLine(inputFile1 + " size != " + inputFile2 + " size");
                return false;
            }

            byte[] buffer1 = new byte[128 * 1024];
            byte[] buffer2 = new byte[128 * 1024];
            while (true)
            {
                int count = inputStream1.Read(buffer1, 0, buffer1.Length);
                if (count == 0)
                    break;

                if (inputStream2.Read(buffer2, 0, count) != count)
                {
                    Console.WriteLine(inputFile1 + " != " + inputFile2);
                    return false;
                }

                for (int i = 0; i < count; i++)
                {
                    if (buffer1[i] != buffer2[i])
                    {
                        Console.WriteLine(inputFile1 + " != " + inputFile2);
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
