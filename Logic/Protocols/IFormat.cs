using System;
using System.Collections.Generic;
using System.Text;

namespace Peeper.Logic.Protocols
{
    public static class Formatting
    {
        private static readonly IFormat USIFormatter = new USIFormat();
        private static readonly IFormat UCIFormatter = new UCIFormat();

        public static IFormat ActiveFormatter = USIFormatter;
        public static bool IsCutechess { get; private set; } = false;

        public static void SetUSIFormatter()
        {
            ActiveFormatter = USIFormatter;
            IsCutechess = false;
        }

        public static void SetUCIFormatter()
        {
            ActiveFormatter = UCIFormatter;
            IsCutechess = true;
        }
    }

    public interface IFormat
    {
        public string FormatSquare(int sq);

        public string FormatMove(Move move);
        public Move ParseMove(string str);

        public string FormatSFen(Position pos);
        public string ParseSFen(string sfen);

        public string FormatMateDistance(int score);

        public string GetGoBlackChar();
        public string GetGoWhiteChar();

        public void SendInitialReadyResponse();
        public string GetReadyResponse();
        public string SetupNewGameCommand();
        public string RespondReadyCommand();
    }
}
