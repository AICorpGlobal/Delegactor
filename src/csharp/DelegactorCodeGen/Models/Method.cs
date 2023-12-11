// Licensed to the AiCorp- Buyconn.

using System.Collections.Generic;

namespace DelegactorCodeGen
{
    public class Method
    {
        public string IsFromReplica { get; set; }
        public string MethodName { get; set; }
        public string ParameterDeclarations { get; set; }
        public List<string> ParametersCollection { get; set; }
        public string ReturnType { get; set; }
        public string IsEnabled { get; set; }
        public string IsBroadcastNotify { get; set; }
    }
}
