// Licensed to the AiCorp- Buyconn.

using System.Reflection;
using System.Text.Json;
using Delegactor.Interfaces;
using Delegactor.Models;

namespace Delegactor.Core
{
    public abstract class ActorBase : IActorBase
    {
        private TimeSpan _activationWindow;
        private string _actorId;

        public ActorBase(string actorId)
        {
            _actorId = actorId;
            _activationWindow = TimeSpan.FromSeconds(5);
        }

        public ActorBase()
        {
            _actorId = Guid.NewGuid().ToString();
            _activationWindow = TimeSpan.FromSeconds(5);
        }

        public int MaxPartitions { get; set; } = 1;

        public virtual string ActorId
        {
            get => _actorId;
            set => _actorId = value;
        }

        public virtual TimeSpan ActivationWindow
        {
            get => _activationWindow;
            set => _activationWindow = value;
        }

        public string Module { get; set; }

        public virtual async Task<ActorResponse> InvokeMethod(ActorRequest request)
        {
            var responseBody = new ActorResponse(request);

            try
            {
                var method = GetType().GetMethod(request.Name);

                var attributes = method.GetCustomAttributes(typeof(ConcurrentMethodAttribute), true).FirstOrDefault() as
                    ConcurrentMethodAttribute;


                var parameters = method.GetParameters();

                var parameterCollection = GetParameterCollection(request, parameters);
                if (attributes == null || attributes.InvokeType == InvokeType.Exclusive)
                {
                    lock (this)
                    {
                        responseBody = InvokeMethod(method, parameterCollection, responseBody);
                    }
                }
                else
                {
                    responseBody = InvokeMethod(method, parameterCollection, responseBody);
                }
            }
            catch (Exception e)
            {
                responseBody.Error = e.Message;
                responseBody.IsError = true;
            }


            return responseBody;
        }

        public virtual async Task OnLoad(ActorRequest actorRequest)
        {
        }

        public virtual async Task OnUnLoad(ActorRequest actorRequest)
        {
        }

        private ActorResponse InvokeMethod(MethodInfo method, object[] parameterCollection, ActorResponse responseBody)
        {
            var result = method.Invoke(this, parameterCollection);

            if (method.ReturnType == typeof(void))
            {
                return responseBody;
            }

            if (method.ReturnType.BaseType != typeof(Task))
            {
                responseBody.Response = JsonSerializer.Serialize(result);
            }
            else
            {
                if (!method.ReturnType.IsGenericType)
                {
                    (result as Task)?.GetAwaiter().GetResult();
                    return responseBody;
                }

                var responseObject = result?.GetType().GetProperty("Result")?.GetValue(result);

                responseBody.Response = JsonSerializer.Serialize(responseObject);
            }

            return responseBody;
        }

        private static object[] GetParameterCollection(ActorRequest request, ParameterInfo[] parameters)
        {
            var parameterCollection = new object[parameters.Length];

            for (var index = 0; index < parameters.Length; index++)
            {
                var parameterValue = request.Parameters.ContainsKey(parameters[index].Name)
                    ? request.Parameters[parameters[index].Name]
                    : string.Empty;

                if (parameters[index].ParameterType.IsPrimitive)
                {
                    if (parameters[index].ParameterType.Name == typeof(string).Name)
                    {
                        parameterCollection[index] = parameterValue;
                    }
                    else if (parameters[index].ParameterType.Name == typeof(Guid).Name)
                    {
                        parameterCollection[index] = Guid.Parse(parameterValue);
                    }
                    else
                    {
                        object numericValue = Convert.ToDouble(parameterValue);
                        parameterCollection[index] =
                            Convert.ChangeType(numericValue, parameters[index].ParameterType);
                    }

                    continue;
                }

                parameterCollection[index] = (parameters[index].ParameterType == typeof(ActorRequest)
                                              && (parameterValue == string.Empty || parameterValue == "null")
                    ? request
                    : JsonSerializer.Deserialize(parameterValue, parameters[index].ParameterType))!;
            }

            return parameterCollection;
        }
    }
}
