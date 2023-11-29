// Licensed to the AiCorp- Buyconn.

using System.Collections.Generic;

namespace DelegactorCodeGen
{
    public class ProxyGenModel
    {
        public string InterfaceName { get; set; }
        public string ClassName { get; set; }
        public string ModuleName { get; set; }
        public string NameSpaceName { get; set; }

        public List<Method> MethodsList { get; set; }
    }
}
