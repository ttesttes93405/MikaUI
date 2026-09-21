

// #define MIKA_UNITASK_SUPPORT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace MikaUI
{


    public interface IMikaAwaiter<TAwaiter> : INotifyCompletion
    {
        public TAwaiter TaskAwaiter { get; }
        public bool IsCompleted { get; }
    }

    public readonly struct MikaAwaiter<T> : IMikaAwaiter<TaskAwaiter<T>>, INotifyCompletion
    {
        private readonly TaskAwaiter<T> _taskAwaiter;
        public TaskAwaiter<T> TaskAwaiter => _taskAwaiter;
        public bool IsCompleted => _taskAwaiter.IsCompleted;

        public MikaAwaiter(in Task<T> task)
        {
            _taskAwaiter = task.GetAwaiter();
        }

        public void OnCompleted(Action continuation)
        {
            _taskAwaiter.OnCompleted(continuation);
        }

        public T GetResult()
        {
            return _taskAwaiter.GetResult();
        }
    }

    public readonly struct MikaAwaiter : IMikaAwaiter<TaskAwaiter>, INotifyCompletion
    {
        private readonly TaskAwaiter _taskAwaiter;
        public TaskAwaiter TaskAwaiter => _taskAwaiter;

        public MikaAwaiter(in Task task)
        {
            _taskAwaiter = task.GetAwaiter();
        }

        public bool IsCompleted => _taskAwaiter.IsCompleted;

        public void OnCompleted(Action continuation)
        {
            _taskAwaiter.OnCompleted(continuation);
        }

        public void GetResult() { }
    }


    [AsyncMethodBuilder(typeof(MikaTaskMethodBuilder<>))]
    public readonly struct MikaTask<T>
    {
        private readonly Task<T> _task;

        public MikaTask(Task<T> task)
        {
            _task = task;
        }

        public MikaTask(Task task)
        {
            _task = task.ContinueWith(t => default(T));
        }

        public MikaAwaiter<T> GetAwaiter()
        {
            return new MikaAwaiter<T>(_task);
        }

        public T WaitResult()
        {
            if (_task.IsCompleted)
                return _task.Result;

            return _task.GetAwaiter().GetResult();
        }

        public static MikaTask<T> FromResult(T result) => new MikaTask<T>(Task.FromResult(result));

        public Task<T> ToTask() => _task;

    }

    [AsyncMethodBuilder(typeof(MikaTaskMethodBuilder))]
    public readonly struct MikaTask
    {
        private readonly Task _task;
        public MikaTask(Task task)
        {
            _task = task;
        }
        public static MikaTask CompletedTask { get; } = new MikaTask(Task.CompletedTask);

        public MikaAwaiter GetAwaiter()
        {
            return new MikaAwaiter(_task);
        }

        public static MikaTask WhenAll(IEnumerable<MikaTask> tasks)
        {
            return new MikaTask(Task.WhenAll(tasks.Select(t => t._task)));
        }

        public Task ToTask() => _task;


    }

    public struct MikaTaskMethodBuilder<T>
    {
        private AsyncTaskMethodBuilder<T> _builder;

        public static MikaTaskMethodBuilder<T> Create() => new() { _builder = AsyncTaskMethodBuilder<T>.Create() };

        public MikaTask<T> Task => new MikaTask<T>(_builder.Task);

        public void SetResult(T result) => _builder.SetResult(result);
        public void SetException(Exception e) => _builder.SetException(e);

        public void SetStateMachine(IAsyncStateMachine stateMachine) => _builder.SetStateMachine(stateMachine);

        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
            => _builder.Start(ref stateMachine);

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
            => _builder.AwaitOnCompleted(ref awaiter, ref stateMachine);

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
            => _builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
    }

    public struct MikaTaskMethodBuilder
    {
        private AsyncTaskMethodBuilder _builder;

        public static MikaTaskMethodBuilder Create() => new() { _builder = AsyncTaskMethodBuilder.Create() };

        public MikaTask Task => new(_builder.Task);

        public void SetResult() => _builder.SetResult();
        public void SetException(Exception e) => _builder.SetException(e);

        public void SetStateMachine(IAsyncStateMachine stateMachine) => _builder.SetStateMachine(stateMachine);

        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
            => _builder.Start(ref stateMachine);

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
            => _builder.AwaitOnCompleted(ref awaiter, ref stateMachine);

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
            => _builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
    }

}