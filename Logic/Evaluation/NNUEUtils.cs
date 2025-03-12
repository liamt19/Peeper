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

        public static Stream TryOpenFile(string networkToLoad, bool exitIfFail = true)
        {
            if (File.Exists(networkToLoad))
            {
                Log($"Loading {networkToLoad} via filepath");
                return File.OpenRead(networkToLoad);
            }

            //  Try to load the default network
            networkToLoad = NNUE.NetworkName;

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


        private const int HAND_MAX_HELD = 38;
        private const int TYPE_STRIDE = SquareNB;
        private const int HAND_STRIDE = TYPE_STRIDE * PieceNB;
        private const int COLOR_STRIDE = HAND_STRIDE + HAND_MAX_HELD;
        public static int BoardFeatureIndexSingle(int color, int type, int sq, int perspective)
        {
            type = PeeperToStoat(type);
            sq = Orient(sq, perspective);

            return (((color ^ perspective) * COLOR_STRIDE) + (type * TYPE_STRIDE) + sq) * L1_SIZE;
        }

        public static int HandFeatureIndexSingle(int handColor, int type, int held, int perspective)
        {
            return (((handColor ^ perspective) * COLOR_STRIDE) + HAND_STRIDE + HandOffsets[type] + held) * L1_SIZE;
        }

        public static (int bIdx, int wIdx) BoardFeatureIndex(int color, int type, int sq)
        {
            type = PeeperToStoat(type);

            int bSq = Orient(sq, Black);
            int wSq = Orient(sq, White);

            var b = ((color ^ Black) * COLOR_STRIDE) + (type * TYPE_STRIDE) + bSq;
            var w = ((color ^ White) * COLOR_STRIDE) + (type * TYPE_STRIDE) + wSq;
            return (b * L1_SIZE, w * L1_SIZE);
        }

        public static (int bIdx, int wIdx) HandFeatureIndex(int handColor, int type, int held)
        {
            var b = ((handColor ^ Black) * COLOR_STRIDE) + HAND_STRIDE + HandOffsets[type] + held;
            var w = ((handColor ^ White) * COLOR_STRIDE) + HAND_STRIDE + HandOffsets[type] + held;
            return (b * L1_SIZE, w * L1_SIZE);
        }


        private const int OOB = int.MaxValue;
        private static ReadOnlySpan<int> HandOffsets => 
        [
            0, 18, 22, 26, 34, 36,
            OOB, OOB, OOB, OOB, OOB, OOB,
            30, OOB
        ];

        public static int PeeperToStoat(int type)
        {
            return type switch
            {
                Pawn => 0,
                PawnPromoted => 1,
                Lance => 2,
                Knight => 3,
                LancePromoted => 4,
                KnightPromoted => 5,
                Silver => 6,
                SilverPromoted => 7,
                Gold => 8,
                Bishop => 9,
                Rook => 10,
                BishopPromoted => 11,
                RookPromoted => 12,
                King => 13,
                None => 14,
                _ => 14
            };
        }
    }
}
