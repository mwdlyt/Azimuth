using Azimuth.Services;
using Azimuth.ViewModels;

namespace Azimuth.Commands;

/// <summary>
/// Undoable command that captures a source move from one position to another.
/// Created on drag-end with the positions captured at drag-start and drag-end.
/// </summary>
public sealed class MoveSourceCommand : IUndoableCommand
{
    private readonly AudioSourceViewModel _sourceVm;
    private readonly double _oldX;
    private readonly double _oldY;
    private readonly double _newX;
    private readonly double _newY;
    private readonly Action<AudioSourceViewModel> _updatePosition;

    public string Description => $"Move {_sourceVm.Name}";

    /// <param name="sourceVm">The source view model to move.</param>
    /// <param name="oldX">X position before the drag.</param>
    /// <param name="oldY">Y position before the drag.</param>
    /// <param name="newX">X position after the drag.</param>
    /// <param name="newY">Y position after the drag.</param>
    /// <param name="updatePosition">Callback to sync the position to the audio engine.</param>
    public MoveSourceCommand(
        AudioSourceViewModel sourceVm,
        double oldX, double oldY,
        double newX, double newY,
        Action<AudioSourceViewModel> updatePosition)
    {
        _sourceVm = sourceVm;
        _oldX = oldX;
        _oldY = oldY;
        _newX = newX;
        _newY = newY;
        _updatePosition = updatePosition;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        _sourceVm.X = _newX;
        _sourceVm.Y = _newY;
        _updatePosition(_sourceVm);
    }

    /// <inheritdoc/>
    public void Undo()
    {
        _sourceVm.X = _oldX;
        _sourceVm.Y = _oldY;
        _updatePosition(_sourceVm);
    }
}
