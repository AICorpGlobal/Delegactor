// Licensed to the AiCorp- Buyconn.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// Read
// https://andrewlock.net/creating-a-source-generator-part-6-saving-source-generator-output-in-source-control/
namespace DelegactorCodeGen
{
    public static class ProxyGenCodeTemplate
    {
        private static string _template = """"
// Licensed to the AiCorp- Buyconn.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Delegactor.Core;
using Delegactor.CodeGen;
using Delegactor.Interfaces;
using Delegactor.Models;
using Microsoft.Extensions.Logging;
 
{{for entry in namespacenamecollection}}
            
{{usingdirective}}

{{end}}

namespace {{namespacename}}
{
    public class {{classname}}ClientProxy : {{interfacename}}, IDelegactorProxy<{{interfacename}}>
    {
        private string _actorId;
        private readonly ILogger<{{classname}}ClientProxy> _logger;
        private readonly IActorSystemTransport _transport;
        
        private string _module = "{{modulename}}";

        public {{classname}}ClientProxy(
            IActorSystemTransport transport,
            ILogger<{{classname}}ClientProxy> logger)
        {
            _transport = transport;
            _logger = logger;
        }

        public string ActorId
        {
            get => _actorId;
            set => _actorId = value;
        }
        

{{for method in methodslist}}

        public async Task{{method.returntype}} {{method.methodname}} {{method.parameterdeclarations}}
        {
            string invokedMethodName = "{{method.methodname}}";
            

            ActorRequest __request = new ActorRequest
            {
                CorrelationId = Guid.NewGuid().ToString(),
                ActorId = _actorId,
                Name = invokedMethodName,
                Module = _module,
                PartitionType = "{{method.isfromreplica}}"
            };

            Dictionary<string, string> keyValuePairs = new Dictionary<string, string>();
           
{{for parameter in method.parameterscollection}}
            
            keyValuePairs.Add( "{{parameter}}", {{parameter}} == default ? string.Empty : JsonSerializer.Serialize({{parameter}}) );

{{end}}
            
            __request.Parameters = keyValuePairs;

{{if method.returntype equals "" }}

            bool noWait = true;

{{else}}

            bool noWait = false;
            
{{endif}} 

{{if method.isbroadcastnotify equals true }}

            ActorResponse resp = await _transport.SendBroadCastNotify(__request);

{{else}}
            ActorResponse resp = await _transport.SendRequest(__request, noWait);  
            
           
            if (resp.IsError)
            {
                throw new AggregateException(resp.Error);
            }

{{endif}}

/*{{method.returntype}}*/

{{if method.returntype equals "" }}

            return;

{{else}}

            return JsonSerializer.Deserialize{{method.returntype}}(resp.Response);
            
{{endif}}

        }
        
{{end}}

    }
}

"""";

        public static string Generate(ProxyGenModel interfaceDefinitions)
        {
            List<Dictionary<string, object>> methodList = new List<Dictionary<string, object>>();
            foreach (var method in interfaceDefinitions.MethodsList)
            {
                var parameterCollections = method.ParametersCollection.Distinct()
                    .Select(x => new Dictionary<string, object> { { "parameter", x.Trim() } }).ToList();
                Dictionary<string, object> detail = new Dictionary<string, object>()
                {
                    { "method.isfromreplica", method.IsFromReplica.Trim() },
                    { "method.isenabled", method.IsEnabled.Trim() },
                    { "method.isbroadcastnotify", method.IsBroadcastNotify.Trim() },
                    { "method.methodname", method.MethodName.Trim() },
                    { "method.parameterdeclarations", method.ParameterDeclarations },
                    { "method.parameterscollection", parameterCollections },
                    { "method.returntype", CleanNewLine(method.ReturnType.Trim()) },
                };
                methodList.Add(detail);
            }

            Dictionary<string, object> keyCollections = new Dictionary<string, object>()
            {
                { "interfacename", interfaceDefinitions.InterfaceName.Trim() },
                { "namespacenamecollection", interfaceDefinitions.NameSpaceNameCollection.Distinct()
                    .Select(x => new Dictionary<string, object> { { "usingdirective", x.Trim() } }).ToList() },
                { "classname", interfaceDefinitions.ClassName.Trim() },
                { "modulename", CleanNewLine(interfaceDefinitions.ModuleName.Trim()) },
                { "namespacename", CleanNewLine(interfaceDefinitions.NameSpaceName.Trim()) },
                { "methodslist", methodList },
            };


            var stringBuilder = new StringBuilder();

            var procesingTracker = 0;

            ProcessBlock(_template, procesingTracker, stringBuilder, keyCollections);

            return stringBuilder.ToString();
        }
        public static string ReplaceStartTokenOf(this string returnType, string token, string subsititute)
        {
            return returnType.StartsWith(token.CleanNewLine()) ? $"{subsititute}{returnType.Substring(token.Length, returnType.Length - token.Length)}" : returnType;
        }

        public static string CleanNewLine(this string input)
        {
            char[] output = new char[input.Length];

            for (int i = 0, j = 0; i < input.Length; i++)
            {
                if (input[i] == '\n' || input[i] == '\r')
                {
                    continue;
                }

                output[j] = input[i];
                j++;
            }

            return new string(output);
        }

        public static int ProcessBlock(string buffer, int processingTracker, StringBuilder writer,
            Dictionary<string, object> keyCollections)
        {
            int index = processingTracker;
            while (index < buffer.Length)
            {
                var statement = ReadStatement(buffer, index == 0 ? 1 : index);

                Console.WriteLine($"got statement {statement.StatementText}");

                if (statement.Found)
                {
                    var tokens = statement.StatementText.Trim('{', '}')
                        .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string token = tokens[0];
                    var tryGetValue = keyCollections.TryGetValue(token, out var valueData);

                    writer.Append(buffer.Substring(index, statement.StartIndex - index));

                    if (tryGetValue)
                    {
                        writer.Append(valueData);
                    }

                    switch (token)
                    {
                        case "for":
                            index = ForLoopHandler(buffer, writer, keyCollections, tokens, statement);
                            continue;
                        case "if":
                            index = IfBlockHandler(buffer, writer, keyCollections, statement);
                            continue;
                    }
                }
                else
                {
                    writer.Append(buffer.Substring(index, statement.EndIndex - index));
                }


                index = statement.EndIndex;
            }

            return index;
        }

        public static int IfBlockHandler(string buffer, StringBuilder writer,
            Dictionary<string, object> keyCollections,
            ReadResult statement)
        {
            if (keyCollections.ContainsKey("string.Empty") == false)
            {
                keyCollections.Add("string.Empty", "");
            }

            //read block using stack
            var readBlock = ReadIfBlock(buffer, statement.StartIndex);

            var startIndex = statement.StatementText.Length;

            var lastIndexOf =
                readBlock.StatementText.LastIndexOf("{{endif}}", StringComparison.Ordinal) + "{{endif}}".Length;

            var statementTextLength = lastIndexOf - startIndex;


            var statementText = readBlock.StatementText.Substring(startIndex, statementTextLength);

            Console.WriteLine($"got if-block {readBlock.StatementText}");

            var ifStatement = ReadStatement(readBlock.StatementText, 0);

            var statementTokens = ifStatement.StatementText.Trim('{', '}')
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            string operand = statementTokens[2];

            var lhs = keyCollections.TryGetValue(statementTokens[1], out var valueData)
                ? valueData.ToString().Trim()
                : statementTokens[1].Trim();
            var rhs = keyCollections.TryGetValue(statementTokens[3], out var valueDataRhs)
                ? valueDataRhs.ToString().Trim()
                : statementTokens[3].Trim();

            bool conditionalResult = false;

            lhs = lhs == "\"\"" ? string.Empty : lhs;
            rhs = rhs == "\"\"" ? string.Empty : rhs;

            switch (operand)
            {
                case "equals":
                    conditionalResult = lhs.ToString().Trim().Equals(rhs.ToString().Trim());
                    break;

                case "not":
                    conditionalResult = !(lhs.ToString().Trim().Equals(rhs.ToString().Trim()));
                    break;
            }

            var outParts = statementText.Split(new string[] { "{{else}}" }, StringSplitOptions.RemoveEmptyEntries);
            outParts = outParts.Length <= 1 ? new[] { outParts[0], string.Empty, } : outParts;

            var output = conditionalResult ? outParts[0] : outParts[1];

            ProcessBlock(output, 0, writer, keyCollections);

            return statement.StartIndex + readBlock.StatementText.Length;
        }

        public static int ForLoopHandler(string buffer, StringBuilder writer,
            Dictionary<string, object> keyCollections, string[] tokens,
            ReadResult statement)
        {
            var keyCollection = keyCollections[tokens[3]] as List<Dictionary<string, object>>;
            //read block using stack
            var loopStatement = ReadLoopBlock(buffer, statement.StartIndex);

            var startIndex = statement.StatementText.Length;
            var lastIndexOf =
                    loopStatement.StatementText.LastIndexOf("{{end}}") + "{{end}}".Length;

            var statementTextLength = lastIndexOf - startIndex;


            var statementText = loopStatement.StatementText.Substring(startIndex, statementTextLength);

            Console.WriteLine($"got loopBlock {loopStatement.StatementText}");

            foreach (var item in keyCollection)
            {
                ProcessBlock(statementText, 0, writer, item);
            }

            return statement.StartIndex + loopStatement.StatementText.Length;
        }

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

        public static ReadResult ReadLoopBlock(string buffer,
            int startPosition)
        {
            string startMarker = "for";
            string endMarker = "end";
            Stack<string> operatorStack = new Stack<string>();
            var index = startPosition;

            ReadResult statement;
            do
            {
                statement = ReadStatement(buffer, index == 0 ? 1 : index);

                if (statement.Found)
                {
                    var tokens = statement.StatementText.Trim('{', '}')
                        .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string token = tokens[0];

                    switch (token)
                    {
                        case "for":
                            operatorStack.Push(token);
                            break;
                        case "end":
                            if (operatorStack.Count > 0)
                            {
                                operatorStack.Pop();
                            }

                            break;
                    }
                }

                index = statement.EndIndex;
            } while (index < buffer.Length && operatorStack.Count > 0);


            var statementText = buffer.Substring(startPosition, index - startPosition);


            return operatorStack.Count == 0
                ? new ReadResult(statementText, true, startPosition, index)
                : new ReadResult("", false, startPosition, index);
        }


        public static ReadResult ReadIfBlock(string buffer,
            int startPosition)
        {
            Stack<string> operatorStack = new Stack<string>();
            var index = startPosition;

            ReadResult statement;
            do
            {
                statement = ReadStatement(buffer, index == 0 ? 1 : index);

                if (statement.Found)
                {
                    var tokens = statement.StatementText.Trim('{', '}')
                        .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string token = tokens[0];

                    switch (token)
                    {
                        case "if":
                            operatorStack.Push(token);
                            break;
                        case "endif":
                            if (operatorStack.Count > 0)
                            {
                                operatorStack.Pop();
                            }

                            break;
                    }
                }

                index = statement.EndIndex;
            } while (index < buffer.Length && operatorStack.Count > 0);


            var statementText = buffer.Substring(startPosition, index - startPosition);


            return operatorStack.Count == 0
                ? new ReadResult(statementText, true, startPosition, index)
                : new ReadResult("", false, startPosition, index);
        }


        private static bool SafeWrap(string buffer, int position, Func<string, int, bool> callback)
        {
            var bufferLength = buffer.Length;
            return position < bufferLength && callback(buffer, position);
        }

        private static bool SafeWrap(string buffer, int position, Stack<string> stack,
            Func<string, int, Stack<string>, bool> callback)
        {
            return position < buffer.Length && callback(buffer, position, stack);
        }

        private static int SafeWrap(string buffer, int position)
        {
            return position < buffer.Length ? position : buffer.Length;
        }

        private static bool IsNotStartStatementMarker(string buffer, int position)
        {
            return position < "{{".Length || !buffer.Substring(position - "{{".Length, "{{".Length).Equals("{{");
            ;
        }

        private static bool IsNotEndStatementMarker(string buffer, int position)
        {
            return position < "}}".Length || !buffer.Substring(position - "}}".Length, "}}".Length).Equals("}}");
            ;
        }

        public static ReadResult ReadStatement(
            string buffer, int startPosition)
        {
            while (SafeWrap(buffer, startPosition, IsNotStartStatementMarker))
            {
                ++startPosition;
            }

            startPosition -= 2;


            int endPosition = startPosition;

            if (startPosition == buffer.Length)
            {
                return new ReadResult("", false, startPosition, endPosition);
            }

            while (SafeWrap(buffer, endPosition, IsNotEndStatementMarker))
            {
                ++endPosition;
            }

            // endPosition -= 2;

            var endOfStatement = IsNotEndStatementMarker(buffer, SafeWrap(buffer, endPosition));

            return endOfStatement
                ? new ReadResult("", false, startPosition, endPosition)
                : new ReadResult(buffer.Substring(startPosition, endPosition - startPosition), true, startPosition,
                    SafeWrap(buffer, endPosition));
        }
    }
}
