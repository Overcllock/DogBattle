using UnityEngine;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using System;
using game.tasks;

namespace game {

public interface ICommand
{
  uint GetNameHash();
  CommandStatus GetStatus();
  CancellationTokenSource GetCancellationTokenSource();
  bool IsCancelable();
  void ScheduleExecute();
  void Cancel();
}

public enum CommandStatus
{
  None,
  Queued,
  Executing,
  Completed,
  Canceled
}

public class CommandProcessor
{
  const int HISTORY_LIMIT = 20;

  class SyncCommand : ICommand
  {
    string name;
    uint name_hash;

    public System.Action OnExecute;
    public System.Action OnCancel;

    GameTaskManager task_manager;

    CommandStatus status;
    public CommandStatus GetStatus() => status;

    CancellationTokenSource cts = new CancellationTokenSource();
    public CancellationTokenSource GetCancellationTokenSource() => cts;

    public bool IsCancelable() => OnCancel != null;

    public SyncCommand(string name, GameTaskManager task_manager, System.Action OnExecute, System.Action OnCancel = null)
    {
      this.name = name;
      this.name_hash = Hash.CRC32(name);
      this.task_manager = task_manager;
      this.OnExecute = OnExecute;
      this.OnCancel = OnCancel;
    }

    public void ScheduleExecute()
    {
      status = CommandStatus.Queued;
      task_manager.ScheduleSyncTask(Execute, OnExecuteCompleted, cts: this.cts);
    }

    void Execute()
    {
      status = CommandStatus.Executing;
      OnExecute?.Invoke();
    }

    void OnExecuteCompleted(bool canceled)
    {
      if(!canceled)
      {
        status = CommandStatus.Completed;
        return;
      }

      if(canceled && IsCancelable())
        Cancel();
    }

    public void Cancel()
    {
      status = CommandStatus.Canceled;
      OnCancel?.Invoke();
    }

    public uint GetNameHash()
    {
      return name_hash;
    }
  }

  class AsyncCommand : ICommand
  {
    string name;
    uint name_hash;
    bool is_parallel;

    public Func<UniTask> OnExecute;
    public Action OnCancel;

    GameTaskManager task_manager;

    CommandStatus status;
    public CommandStatus GetStatus() => status;

    CancellationTokenSource cts = new CancellationTokenSource();
    public CancellationTokenSource GetCancellationTokenSource() => cts;

    public bool IsCancelable() => OnCancel != null;

    public AsyncCommand(string name, bool is_parallel, GameTaskManager task_manager, Func<UniTask> OnExecute, Action OnCancel = null)
    {
      this.name = name;
      this.name_hash = Hash.CRC32(name);
      this.is_parallel = is_parallel;
      this.task_manager = task_manager;
      this.OnExecute = OnExecute;
      this.OnCancel = OnCancel;
    }

    public void ScheduleExecute()
    {
      status = CommandStatus.Queued;
      task_manager.ScheduleAsyncTask(Execute, OnExecuteCompleted, cts: this.cts, is_parallel: this.is_parallel);
    }

    async UniTask Execute()
    {
      status = CommandStatus.Executing;
      await OnExecute.Invoke();
    }

    void OnExecuteCompleted(bool canceled)
    {
      if(!canceled)
      {
        status = CommandStatus.Completed;
        return;
      }

      if(canceled && IsCancelable())
        Cancel();
    }

    public void Cancel()
    {
      status = CommandStatus.Canceled;
      OnCancel?.Invoke();
    }

    public uint GetNameHash()
    {
      return name_hash;
    }
  }

  Dictionary<uint, ICommand> defined_cmds = new Dictionary<uint, ICommand>();
  Stack<ICommand> cmd_history = new Stack<ICommand>(HISTORY_LIMIT);
  Queue<ICommand> pending_cmds = new Queue<ICommand>();
  List<ICommand> executing_cmds = new List<ICommand>();

  GameTaskManager task_manager = new GameTaskManager();

  public ICommand DefineSyncCommand(string name, Action OnExecute, Action OnCancel = null)
  {
    var cmd = new SyncCommand(name, task_manager, OnExecute, OnCancel);
    defined_cmds.Add(cmd.GetNameHash(), cmd);
    return cmd;
  }

  public ICommand DefineAsyncCommand(string name, bool is_parallel, Func<UniTask> OnExecute, Action OnCancel = null)
  {
    var cmd = new AsyncCommand(name, is_parallel, task_manager, OnExecute, OnCancel);
    defined_cmds.Add(cmd.GetNameHash(), cmd);
    return cmd;
  }

  public void Tick()
  {
    task_manager.Tick();

    UpdateExecutingCommands();

    if(pending_cmds.Count == 0)
      return;
    
    var current_exec_cmd = pending_cmds.Dequeue();
    current_exec_cmd.ScheduleExecute();

    executing_cmds.Add(current_exec_cmd);
  }

  void UpdateExecutingCommands()
  {
    foreach(var cmd in executing_cmds)
    {
      if(cmd.GetStatus() == CommandStatus.Completed)
        cmd_history.Push(cmd);
    }

    executing_cmds.RemoveAll(cmd => cmd.GetStatus() == CommandStatus.Completed);
  }

  public void ExecuteCommand(string name)
  {
    var hash = Hash.CRC32(name);
    if(!defined_cmds.ContainsKey(hash))
    {
      Debug.LogError($"Unable to execute cmd: {name}. Command is not defined.");
      return;
    }

    var cmd = defined_cmds[hash];
    pending_cmds.Enqueue(cmd);
  }

  public ICommand FindCommand(string name)
  {
    var hash = Hash.CRC32(name);
    if(!defined_cmds.ContainsKey(hash))
      return null;
    
    return defined_cmds[hash];
  }

  public bool CommandExists(string name)
  {
    var hash = Hash.CRC32(name);
    return defined_cmds.ContainsKey(hash);
  }

  public void Destroy()
  {
    task_manager.Clear();
  }
}

}