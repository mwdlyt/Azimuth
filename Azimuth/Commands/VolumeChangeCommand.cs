using Azimuth.Services;
using Azimuth.ViewModels;

namespace Azimuth.Commands;

/// <summary>
/// Undoable command that captures a volume change. Created on mouse-up, not every slider tick.
/// </summary>
public sealed class VolumeChangeCommand : IUndoableCommand
{
    private readonly AudioSourceViewModel _sourceVm;
    private readonly float _oldVolume;
    private readonly float _newVolume;
    private readonly Action<AudioSourceViewModel> _updateVolume;

    public string Description => $"Volume {_sourceVm.Name}";

    /// <param name="sourceVm">The source view model.</param>
    /// <param name="oldVolume">Volume before the change (0.0-1.0).</param>
    /// <param name="newVolume">Volume after the change (0.0-1.0).</param>
    /// <param name="updateVolume">Callback to sync volume to the audio engine.</param>
    public VolumeChangeCommand(
        AudioSourceViewModel sourceVm,
        float oldVolume,
        float newVolume,
        Action<AudioSourceViewModel> updateVolume)
    {
        _sourceVm = sourceVm;
        _oldVolume = oldVolume;
        _newVolume = newVolume;
        _updateVolume = updateVolume;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        _sourceVm.BaseVolume = _newVolume;
        _updateVolume(_sourceVm);
    }

    /// <inheritdoc/>
    public void Undo()
    {
        _sourceVm.BaseVolume = _oldVolume;
        _updateVolume(_sourceVm);
    }
}
