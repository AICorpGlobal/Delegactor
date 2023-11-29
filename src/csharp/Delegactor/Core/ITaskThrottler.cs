// Licensed to the AiCorp- Buyconn.

namespace Delegactor.Core
{
    public interface ITaskThrottler<TGroupType> where TGroupType : class
    {
        string GroupTypeName { get; }

        int MaxSize { get; set; }

        Task AddTaskAsync<TIn, TOut>(TIn input,
            Func<TIn, Task<TOut>> asyncCallMethod,
            Func<TIn, TOut, Task> onCompletionCallBack,
            Func<TIn, Exception, Task> onErrorCallBack);
    }
}
