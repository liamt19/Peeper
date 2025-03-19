using System;
using System.Collections.Generic;
using System.Text;

namespace Peeper.Logic.Protocols
{
    public static class Formatting
    {
        public static readonly IFormat USIFormatter = new USIFormat();
        public static readonly IFormat UCIFormatter = new UCIFormat();

        public static IFormat ActiveFormatter = USIFormatter;
        public static bool IsFormatterUSI { get; private set; } = true;

        public static void SetUSIFormatter()
        {
            ActiveFormatter = USIFormatter;
            IsFormatterUSI = true;
        }

        public static void SetUCIFormatter()
        {
            ActiveFormatter = UCIFormatter;
            IsFormatterUSI = false;
        }
    }

    public interface IFormat
    {
        public string FormatSquare(int sq);

        public string FormatMove(Move move);
        public Move ParseMove(string str);

        public string FormatSFen(Position pos);
        public string ParseSFen(string sfen);

        public string DisplayBoard(Bitboard bb);

        public string FormatMateDistance(int score);

        public string GetGoBlackChar();
        public string GetGoWhiteChar();

        public void SendInitialReadyResponse();
        public string GetReadyResponse();
        public string SetupNewGameCommand();
        public string RespondReadyCommand();

        public (string response, bool abort) HandleImpasse();
    }
}
