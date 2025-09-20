

// #define MIKA_UNITASK_SUPPORT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;


#if(MIKA_UNITASK_SUPPORT)
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif


namespace MikaUISystem
{

    public readonly struct MikaAwaiter<T> : INotifyCompletion
    {
#if(MIKA_UNITASK_SUPPORT)
        private readonly UniTask<T>.Awaiter _uniTaskAwaiter;
#else
        private readonly TaskAwaiter<T> _taskAwaiter;
#endif


#if (MIKA_UNITASK_SUPPORT)
        public FlexibleAwaiter(in UniTask<T> uniTask)
        {
            _uniTaskAwaiter = uniTask.GetAwaiter();
        }
#else
        public MikaAwaiter(in Task<T> task)
        {
            _taskAwaiter = task.GetAwaiter();
        }
#endif

        public bool IsCompleted
        {
            get
            {
                return
#if (MIKA_UNITASK_SUPPORT)
                    _uniTaskAwaiter.IsCompleted;
#else
                    _taskAwaiter.IsCompleted;
#endif
            }
        }

        public void OnCompleted(Action continuation)
        {
#if (MIKA_UNITASK_SUPPORT)
            _uniTaskAwaiter.OnCompleted(continuation);
#else
            _taskAwaiter.OnCompleted(continuation);
#endif
        }

        public T GetResult()
        {
            return
#if (MIKA_UNITASK_SUPPORT)
                _uniTaskAwaiter.GetResult();
#else
                _taskAwaiter.GetResult();
#endif
        }
    }

    public readonly struct MikaAwaiter : INotifyCompletion
    {
#if(MIKA_UNITASK_SUPPORT)
        private readonly UniTask.Awaiter _uniTaskAwaiter;
#else
        private readonly TaskAwaiter _taskAwaiter;
#endif


#if (MIKA_UNITASK_SUPPORT)
        public FlexibleAwaiter(in UniTask uniTask)
        {
            _uniTaskAwaiter = uniTask.GetAwaiter();
        }
#else
        public MikaAwaiter(in Task task)
        {
            _taskAwaiter = task.GetAwaiter();
        }
#endif

        public bool IsCompleted
        {
            get
            {
                return
#if (MIKA_UNITASK_SUPPORT)
                    _uniTaskAwaiter.IsCompleted;
#else
                    _taskAwaiter.IsCompleted;
#endif
            }
        }

        public void OnCompleted(Action continuation)
        {
#if (MIKA_UNITASK_SUPPORT)
            _uniTaskAwaiter.OnCompleted(continuation);
#else
            _taskAwaiter.OnCompleted(continuation);
#endif
        }
        public void GetResult() { }
    }

    [AsyncMethodBuilder(typeof(MikaTaskMethodBuilder<>))]
    public readonly struct MikaTask<T>
    {
#if (MIKA_UNITASK_SUPPORT)
        private readonly UniTask<T> _uniTask;
#else
        private readonly Task<T> _task;
#endif

#if (MIKA_UNITASK_SUPPORT)
        public MikaTask(UniTask<T> uniTask)
        {
            _uniTask = uniTask;
        }
#else
        public MikaTask(Task<T> task)
        {
            _task = task;
        }
#endif

        public MikaAwaiter<T> GetAwaiter()
        {
            return
#if (MIKA_UNITASK_SUPPORT)
                new MikaAwaiter<T>(_uniTask);
#else
                new MikaAwaiter<T>(_task);
#endif
        }


        public static MikaTask<T> FromResult(T result)
        {
            return
#if (MIKA_UNITASK_SUPPORT)
                new MikaTask<T>(UniTask.FromResult(result));
#else
                new MikaTask<T>(Task.FromResult(result));
#endif
        }

    }

    [AsyncMethodBuilder(typeof(MikaTaskMethodBuilder))]
    public readonly struct MikaTask
    {
#if (MIKA_UNITASK_SUPPORT)
        private readonly UniTask _uniTask;
#else
        private readonly Task _task;
#endif
#if (MIKA_UNITASK_SUPPORT)
        public MikaTask(UniTask uniTask)
        {
            _uniTask = uniTask;
        }
#else
        public MikaTask(Task task)
        {
            _task = task;
        }
#endif
        public static MikaTask CompletedTask
        {
            get
            {
                return
#if (MIKA_UNITASK_SUPPORT)
                    new MikaTask(UniTask.CompletedTask);
#else
                    new MikaTask(Task.CompletedTask);
#endif
            }
        }

        public MikaAwaiter GetAwaiter()
        {
            return
#if (MIKA_UNITASK_SUPPORT)
                new MikaAwaiter(_uniTask);
#else
                new MikaAwaiter(_task);
#endif
        }

        public static MikaTask WhenAll(IEnumerable<MikaTask> tasks)
        {
            return
#if (MIKA_UNITASK_SUPPORT)
                new MikaTask(UniTask.WhenAll(tasks.Select(t => t._uniTask)));
#else
                new MikaTask(Task.WhenAll(tasks.Select(t => t._task)));
#endif
        }
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