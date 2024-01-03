// Licensed to the AiCorp- Buyconn.

namespace Delegactor.Core
{
    public interface ITaskThrottler<TGroupType> where TGroupType : class
    {
        int MaxSize { get; set; }

        Task AddTaskAsync(Func<Task> asyncCallMethod);
    }
}
