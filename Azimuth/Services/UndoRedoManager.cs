namespace Azimuth.Services;

/// <summary>
/// Represents a command that can be executed and undone.
/// </summary>
public interface IUndoableCommand
{
    /// <summary>Gets a human-readable description of this command.</summary>
    string Description { get; }

    /// <summary>Executes the command.</summary>
    void Execute();

    /// <summary>Reverses the command.</summary>
    void Undo();
}

/// <summary>
/// Manages undo/redo stacks for undoable commands. Max 100 entries.
/// </summary>
public sealed class UndoRedoManager
{
    private const int MaxStackSize = 100;

    private readonly Stack<IUndoableCommand> _undoStack = new();
    private readonly Stack<IUndoableCommand> _redoStack = new();

    /// <summary>Raised when the undo/redo state changes.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Gets whether there are commands to undo.</summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>Gets whether there are commands to redo.</summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Executes a command and pushes it onto the undo stack, clearing the redo stack.
    /// </summary>
    public void Execute(IUndoableCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();

        // Enforce max stack size
        if (_undoStack.Count > MaxStackSize)
        {
            TrimStack(_undoStack);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Undoes the most recent command.
    /// </summary>
    public void Undo()
    {
        if (!CanUndo) return;
        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Redoes the most recently undone command.
    /// </summary>
    public void Redo()
    {
        if (!CanRedo) return;
        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Clears both undo and redo stacks.
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Trims the stack to the max size by removing the oldest entries.
    /// </summary>
    private static void TrimStack(Stack<IUndoableCommand> stack)
    {
        var items = stack.ToArray();
        stack.Clear();
        // items[0] is top (newest), items[^1] is bottom (oldest)
        // Keep only the newest MaxStackSize items
        for (int i = Math.Min(items.Length, MaxStackSize) - 1; i >= 0; i--)
        {
            stack.Push(items[i]);
        }
    }
}
