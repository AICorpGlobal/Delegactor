// Licensed to the AiCorp- Buyconn.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Delegactor.Core
{
    public class TaskThrottler<TGroupType> : ITaskThrottler<TGroupType> where TGroupType : class
    {
        private readonly ILogger<TaskThrottler<TGroupType>> _logger;
        private readonly Semaphore _semControl;
        private readonly ConcurrentDictionary<string, Task> _taskPool = new();

        public TaskThrottler(ILogger<TaskThrottler<TGroupType>> logger)
        {
            _logger = logger;
            _semControl = new Semaphore(MaxSize, MaxSize + 1);
        }

        public string GroupTypeName => typeof(TGroupType).FullName;
        public int MaxSize { get; set; } = Environment.ProcessorCount * 4;

        public Task AddTaskAsync<TIn, TOut>(TIn input,
            Func<TIn, Task<TOut>> asyncCallMethod,
            Func<TIn, TOut, Task> onCompletionCallBack,
            Func<TIn, Exception, Task> onErrorCallBack)
        {
            var taskUid = Guid.NewGuid().ToString();
            _semControl.WaitOne();
            var task = Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation(" Task {TaskUid} Executiong started for {GroupTypeName}", taskUid,
                        GroupTypeName);

                    var output = await asyncCallMethod(input);
                    await onCompletionCallBack(input, output);
                    if (_taskPool.TryRemove(taskUid, out var taskOut))
                    {
                        _logger.LogInformation(" Task has been removed {TaskUid}", taskUid);
                    }
                    else
                    {
                        _logger.LogInformation(" Task removal failed {TaskUid} ", taskUid);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInformation("Error occured in task, Invoking on error callback {ExMessage}", ex.Message);
                    await onErrorCallBack(input, ex);
                }
                finally
                {
                    _semControl.Release();
                }
            });
            _taskPool.TryAdd(taskUid, task);
            return Task.CompletedTask;
        }
    }
}
