using Azimuth.Models;
using Azimuth.Services;
using Azimuth.ViewModels;

namespace Azimuth.Commands;

/// <summary>
/// Undoable command that adds an audio source to the scene.
/// </summary>
public sealed class AddSourceCommand : IUndoableCommand
{
    private readonly AudioSource _sourceData;
    private readonly AudioSourceViewModel _sourceVm;
    private readonly Action<AudioSourceViewModel> _addAction;
    private readonly Action<AudioSourceViewModel> _removeAction;

    public string Description => $"Add {_sourceData.Name}";

    /// <param name="sourceData">The audio source model data.</param>
    /// <param name="sourceVm">The view model for the source.</param>
    /// <param name="addAction">Callback to add the source to the collection and engine.</param>
    /// <param name="removeAction">Callback to remove the source from the collection and engine.</param>
    public AddSourceCommand(
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
        _addAction(_sourceVm);
    }

    /// <inheritdoc/>
    public void Undo()
    {
        _removeAction(_sourceVm);
    }
}
