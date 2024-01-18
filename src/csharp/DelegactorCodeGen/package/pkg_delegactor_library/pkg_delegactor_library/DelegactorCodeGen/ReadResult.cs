// Licensed to the AiCorp- Buyconn.

namespace DelegactorCodeGen
{
    public class ReadResult
    {
        public ReadResult(string statementText, bool found, int startIndex, int endIndex)
        {
            StatementText = statementText;
            Found = found;
            StartIndex = startIndex;
            EndIndex = endIndex;
        }

        public string StatementText { get; }
        public bool Found { get; }
        public int StartIndex { get; }
        public int EndIndex { get; }
    }
}
