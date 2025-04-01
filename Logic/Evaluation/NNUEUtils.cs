using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

using static Peeper.Logic.Evaluation.NNUE;

namespace Peeper.Logic.Evaluation
{
    public static unsafe class NNUEUtils
    {

        public static Stream TryOpenFile(string? networkToLoad = null, bool exitIfFail = true)
        {
            if (networkToLoad is null)
            {
                try
                {
                    networkToLoad = Assembly.GetEntryAssembly()?.GetCustomAttribute<EvalFileAttribute>()?.EvalFile.Trim();
                }
                catch { networkToLoad = ""; }
            }

            if (File.Exists(networkToLoad))
            {
                Log($"Loading {networkToLoad} via filepath");
                return File.OpenRead(networkToLoad);
            }

            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                foreach (string res in asm.GetManifestResourceNames())
                {
                    //  Specifically exclude the .resx file
                    if (res.ToLower().Contains("properties"))
                        continue;

                    if (!res.EndsWith(".dll") && !res.EndsWith(".so") && !res.Contains("HorsieBindings"))
                    {
                        Stream stream = asm.GetManifestResourceStream(res);
                        if (stream != null)
                        {
                            Log($"Loading {res} via reflection");
                            return stream;
                        }
                    }
                }
            }
            catch { }

            //  Then look for it as an absolute path
            if (File.Exists(networkToLoad))
            {
                Log($"Loading {networkToLoad} via absolute path");
                return File.OpenRead(networkToLoad);
            }

            //  Lastly try looking for it in the current directory
            var cwdFile = Path.Combine(Environment.CurrentDirectory, networkToLoad);
            if (File.Exists(cwdFile))
            {
                Log($"Loading {networkToLoad} via relative path");
                return File.OpenRead(cwdFile);
            }


            Console.WriteLine($"Couldn't find a network named '{networkToLoad}' or as a compiled resource or as a file within the current directory!");
            Console.ReadLine();

            if (exitIfFail)
                Environment.Exit(-1);

            return null;
        }

        public static void TransposeLayerWeights(short* block, int columnLength, int rowLength)
        {
            short* temp = stackalloc short[columnLength * rowLength];
            Unsafe.CopyBlock(temp, block, (uint)(sizeof(short) * columnLength * rowLength));

            for (int bucket = 0; bucket < rowLength; bucket++)
            {
                short* thisBucket = block + (bucket * columnLength);

                for (int i = 0; i < columnLength; i++)
                {
                    thisBucket[i] = temp[(rowLength * i) + bucket];
                }
            }
        }

        public static int Orient(int sq, int perspective)
        {
            return (perspective == White) ? A1 - sq : sq;
        }
    }
}
