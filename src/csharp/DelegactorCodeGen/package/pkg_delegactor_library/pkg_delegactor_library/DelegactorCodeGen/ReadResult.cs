// Licensed to the AiCorp- Buyconn.

namespace DelegactorCodeGen
{
    public class ReadResult
    {
        public string StatementText { get; }
        public bool Found { get; }
        public int StartIndex { get; }
        public int EndIndex { get; }

        public ReadResult(string statementText, bool found, int startIndex, int endIndex)
        {
            this.StatementText = statementText;
            this.Found = found;
            this.StartIndex = startIndex;
            this.EndIndex = endIndex;
        }
    }
}
