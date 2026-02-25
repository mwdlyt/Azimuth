using Azimuth.Models;
using Azimuth.Services;
using Azimuth.ViewModels;

namespace Azimuth.Commands;

/// <summary>
/// Undoable command that removes an audio source from the scene.
/// Captures the full source state so it can be re-added on undo.
/// </summary>
public sealed class RemoveSourceCommand : IUndoableCommand
{
    private readonly AudioSource _sourceData;
    private readonly AudioSourceViewModel _sourceVm;
    private readonly Action<AudioSourceViewModel> _addAction;
    private readonly Action<AudioSourceViewModel> _removeAction;

    public string Description => $"Remove {_sourceData.Name}";

    /// <param name="sourceData">The audio source model data.</param>
    /// <param name="sourceVm">The view model for the source.</param>
    /// <param name="addAction">Callback to re-add the source on undo.</param>
    /// <param name="removeAction">Callback to remove the source on execute.</param>
    public RemoveSourceCommand(
        AudioSource sourceData,
        AudioSourceViewModel sourceVm,
        Action<AudioSourceViewModel> addAction,
        Action<AudioSourceViewModel> removeAction)
    {
        _sourceData = sourceData;
        _sourceVm = sourceVm;
        _addAction = addAction;
        _removeAction = removeAction;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        _removeAction(_sourceVm);
    }

    /// <inheritdoc/>
    public void Undo()
    {
        _addAction(_sourceVm);
    }
}
