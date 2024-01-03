// Licensed to the AiCorp- Buyconn.

using System.Collections;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Delegactor.Core
{
    public class TaskThrottler<TGroupType> : ITaskThrottler<TGroupType> where TGroupType : class
    {
        private readonly ILogger<TaskThrottler<TGroupType>> _logger;
        private readonly Semaphore _semControl;

        public TaskThrottler(ILogger<TaskThrottler<TGroupType>> logger)
        {
            _logger = logger;
            _semControl = new Semaphore(MaxSize, MaxSize * 4 * 100);
        }

        public int MaxSize { get; set; } = Environment.ProcessorCount;

        public Task AddTaskAsync(Func<Task> asyncCallMethod)
        {
            _semControl.WaitOne();
            Task.Run(() =>
            {
                try
                {
                    asyncCallMethod();
                }
                catch (Exception e)
                {
                    _logger.LogError(e.Message);
                }

                _semControl.Release();
            });
            return Task.CompletedTask;
        }
    }
}
